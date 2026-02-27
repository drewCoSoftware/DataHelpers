using drewCo.Tools;
using System.Text;
using System.Collections;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Formats.Asn1;
using drewCo.Tools.Logging;

namespace DataHelpers.Data;

internal record NamesAndValues(List<string> ColNames, List<string> ColValues, string? PrimaryKeyName);


// ============================================================================================================================
public class SchemaDefinition
{
  private object ResolveLock = new object();
  private Dictionary<string, TableDef> _TableDefs = new Dictionary<string, TableDef>(StringComparer.OrdinalIgnoreCase);
  private Dictionary<Type, TableDef> TypesToTableDef = new Dictionary<Type, TableDef>();
  public ReadOnlyCollection<TableDef> TableDefs { get { return new ReadOnlyCollection<TableDef>(_TableDefs.Values.ToList()); } }
  public ISqlFlavor Flavor { get; private set; }


  // --------------------------------------------------------------------------------------------------------------------------
  public SchemaDefinition(ISqlFlavor flavor_)
  {
    Flavor = flavor_;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Create a new schema defintion from the given type.  Each of the properties in <paramref name="schemaType"/>
  /// will be used to create a new table in the schema.
  /// </summary>
  public SchemaDefinition(ISqlFlavor flavor_, Type schemaType)
    : this(flavor_)
  {
    // We will add a data set for each of the properties defined in 'schemaType'
    var props = ReflectionTools.GetProperties(schemaType);
    foreach (var prop in props)
    {
      if (!prop.CanWrite) { continue; }

      var useType = prop.PropertyType;
      if (ReflectionTools.HasInterface<IList>(useType))
      {
        useType = useType.GetGenericArguments()[0];
      }

      // NOTE: I think it is a good idea to take a first pass to create all of the named tables
      // BEFORE populating their data.  The thing is that it is possible for their to be tables
      // of the same struture, but just with different names, like in a multi-tenant app.
      // of course, if we cared about multi-tenancy, then this type of schema definition
      // probably would not work in the first place......
      // Such a system would have to be aware of name groupings?
      // --> OK, so multi-tenancy is way overkill, let's just make it so that the various members
      // and relationships are all resolved by type.  Then the first pass of this resolver is made
      // simply to determine the type->name mappings....
      // Anything that doesn't appear at this parent level can't be used.  I am OK with that
      // because I don't really see the need to have sub-type resolvers at this point in time.
      // If we ever needed such a feature, then it would just have to work by detecting the first
      // name->type mapping, and then force all subsequent name->type mappings to be the same?

      // NOTE: Other attributes could be analyzed to change table names, etc.
      // ResolveTableDef(prop.Name, useType);
      InitTableDef(prop.Name, useType);
    }

    PopulateMembers();

    PopulateRelationships();


    ValidateSchema();

    CreatePropertyMap();

  }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Creates the property map that is used during querying for binding data.
  /// </summary>
  private void CreatePropertyMap()
  {
    Log.Warning($"function: {nameof(CreatePropertyMap)} has no implementation!");
    //foreach (var td in this.TableDefs)
    //{
    //  td.CreatePropertyMap();
    //}
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public TableDef? TryGetTableDef<T>()
  {
    if (!TypesToTableDef.TryGetValue(typeof(T), out TableDef? res))
    {
      return null;
    }
    return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public TableDef GetTableDef<T>()
  {
    TableDef res = TryGetTableDef<T>();
    if (res == null)
    {
      throw new InvalidOperationException($"There is no table def for type: {typeof(T)} in this schema!");
    }
    return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public TableDef? GetTableDef(string name, bool allowNull = false)
  {
    _TableDefs.TryGetValue(name, out TableDef? res);
    if (res == null && !allowNull)
    {
      throw new InvalidOperationException($"There is no dataset named: {name} in this schema!");
    }
    return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Get the table def for the matching type.
  /// </summary>
  public TableDef? GetTableDef(Type type, bool allowNull = false)
  {
    if (!this.TypesToTableDef.TryGetValue(type, out TableDef? res))
    {
      if (allowNull)
      {
        return null;
      }
      else
      {
        throw new InvalidOperationException($"There is no table definition for type: {type} in this schema!");
      }
    }
    return res;
  }


  // --------------------------------------------------------------------------------------------------------------------------
  public string GetSelectQuery<T>(Expression<Func<T, bool>>? predicate)
  {
    //string res = null;
    var sb = new StringBuilder(0x800);

    Type t = typeof(T);
    var def = GetTableDef(t, false)!;

    sb.Append($"SELECT * FROM {def.Name}");
    if (predicate != null)
    {
      // This is a where clause, let's get the syntax for it......
      sb.Append(" WHERE ");

      string condition = GetExpressionSyntax(predicate);
      sb.Append(condition);
    }

    string res = sb.ToString();
    return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public object GetParamatersObject<T>(T child)
  {
    // NOTE: We should probably use 'Helpers.CreateDynamicParameters'!
    throw new InvalidOperationException("This function is currently not working and breaking some tests.  review its use + write some standalone test cases, please");
  }

  // --------------------------------------------------------------------------------------------------------------------------
  private string GetExpressionSyntax(Expression predicate)
  {
    Expression useExpression = predicate;
    if (predicate is LambdaExpression)
    {
      useExpression = (predicate as LambdaExpression).Body;
    }

    switch (useExpression.NodeType)
    {
      case ExpressionType.Equal:
        // An equality expression....
        var exp = useExpression as System.Linq.Expressions.BinaryExpression;

        string left = GetExpressionSyntax(exp.Left);
        string right = string.Empty;
        if (exp.Left is MemberExpression)
        {
          string useMemberName = (exp.Left as MemberExpression).Member.Name;
          right = "@" + useMemberName;
        }
        else
        {
          right = GetExpressionSyntax(exp.Right);
        }

        string res = left + " = " + right;
        return res;

      case ExpressionType.MemberAccess:
        return (useExpression as MemberExpression).Member.Name;
        break;

      case ExpressionType.Constant:
        return (useExpression as ConstantExpression).Value.ToString();
        break;

      default:
        throw new NotSupportedException($"There is no support for node type: {predicate.NodeType}");
    }
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public string GetSaveQuery<T>(T instance)
    where T : IHasPrimary
  {
    return GetSaveQuery(typeof(T), instance);
  }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Creates a query that will insert/update data for the given type.
  /// </summary>
  public string GetSaveQuery(Type t, IHasPrimary? instance)
  {
    if (instance == null) { throw new ArgumentNullException(nameof(instance)); }

    var sb = new StringBuilder();

    // Find the matching table def for the type:
    TableDef? tableDef = GetTableDef(t);
    if (tableDef == null)
    {
      throw new KeyNotFoundException($"There is no table definition for type: {t} in this schema!");
    }

    // // Insert all childrens first:
    // // NOTE: We may need to do a dependency resolution at some point, or that can just be part of
    // // the TableDef building process?
    // foreach (var c in tableDef.ChildTables)
    // {
    //   // Get the member that represents the child table/tables.
    //   PropertyInfo propInfo = ReflectionTools.GetPropertyInfo(t, c.PropertyName);
    //   Type useType = t;
    //   bool isList = ReflectionTools.HasInterface<IList>(useType);
    //   if (isList)
    //   {
    //     useType = useType.GetGenericArguments()[0];

    //     // We will create a query for each of the members.
    //     var list = (IList)propInfo.GetValue(instance)!;
    //     foreach (var item in list)
    //     {
    //       string itemQuery = GetInsertUpdateQuery(useType, item as IHasPrimary);
    //       sb.Append(itemQuery + Environment.NewLine);
    //     }
    //   }
    //   else
    //   {
    //     // This is a single instance, so we can just create a normal insert/update query for it.
    //     string itemQuery = GetInsertUpdateQuery(useType, propInfo.GetValue(instance) as IHasPrimary);
    //     sb.Append(itemQuery + Environment.NewLine);
    //   }
    // }


    // Now that the child tables are complete, we can create our query.
    // NOTE: If we had proxied types, we would be able to better determine if we are doing an add/update type query...

    if (instance.ID == 0)
    {
      var columns = new List<string>();
      var values = new List<string>();
      foreach (var c in tableDef.Columns)
      {
        if (c.IsPrimary) { continue; }

        // PropertyInfo colProp = ReflectionTools.GetPropertyInfo(t, c.Name);
        // if (colProp.Name == nameof(IHasPrimary.ID)) { continue; }

        // object? val = colProp.GetValue(instance);
        // if (val == null)
        // {
        //   if (!c.IsNullable)
        //   {
        //     throw new InvalidOperationException($"Column: {c.Name} has a null value, but is not nullable!");
        //   }
        //   // continue;
        // }
        // // else
        // {
        values.Add($"@{c.PropertyName}");
        //        }

        // NOTE: This is where we will check for nulls, default values, etc.
        columns.Add(c.PropertyName);
      }

      // INSERT
      string useColumns = string.Join(",", columns);
      string useValues = string.Join(",", values);
      sb.Append($"INSERT INTO {tableDef.Name} ({useColumns}) VALUES ({useValues});");
    }
    else
    {
      // UPDATE:
      throw new NotImplementedException();
    }

    string res = sb.ToString();
    return res;
  }


  // --------------------------------------------------------------------------------------------------------------------------
  private void PopulateRelationships()
  {
    foreach (var def in _TableDefs.Values)
    {
      // Now we can populate all of the members.
      def.PopulateRelationships();
    }
  }

  // --------------------------------------------------------------------------------------------------------------------------
  private void PopulateMembers()
  {
    foreach (var def in _TableDefs.Values)
    {
      // Now we can populate all of the members.
      def.PopulateMembers();
    }

    var allGeneratedSets = new List<TableDef>();
    foreach (var def in _TableDefs.Values)
    {
      // Now we can populate all of the members.
      var generatedSets = def.PopulateRelationMembers();
      allGeneratedSets.AddRange(generatedSets);
    }

    // Now we can remove all of the temp, related columns from each of the sets:
    foreach (var def in _TableDefs.Values)
    {
      var toRemove = (from x in def.Columns where x.DataType == ColumnDef.RELATION_PLACEHOLDER select x).ToList();
      foreach (var item in toRemove)
      {
        def.RemoveCol(item);
      }
    }

    // Add the generated sets to the Schema def.
    // We filter them first because at time of writing it is possible to double-define them (no way to detect that one has been created in prior step)
    // allGeneratedSets.DistinctBy(x=>x.Name
    allGeneratedSets = GetUniqueSets(allGeneratedSets);


    foreach (var item in allGeneratedSets)
    {
      this.AddMappingSet(item);
    }

  }

  // --------------------------------------------------------------------------------------------------------------------------
  private List<TableDef> GetUniqueSets(List<TableDef> input)
  {
    var res = input;

    var toRemove = new List<TableDef>();
    int len = input.Count;
    for (int i = 0; i < len; i++)
    {
      var src = input[i];
      for (int j = i + 1; j < len; j++)
      {
        var comp = input[j];
        if (src.Name == comp.Name &&
        src.DataType == comp.DataType &&
        ColumnsMatch(src, comp))
        {
          toRemove.Add(comp);
        }
      }
    }
    foreach (var item in toRemove)
    {
      res.Remove(item);
    }
    return res;

  }

  // --------------------------------------------------------------------------------------------------------------------------
  private bool ColumnsMatch(TableDef src, TableDef comp)
  {
    if (src.Columns.Count == comp.Columns.Count)
    {

      int len = src.Columns.Count;
      for (int i = 0; i < len; i++)
      {
        var srcCol = src.Columns[i];
        var compCol = comp.GetColumn(srcCol.PropertyName);
        if (compCol == null) { return false; }

        // We can get even deeper into matching here if we want, but this should be OK for now...
        // if (compCol.rel
        return true;
      }


    }

    return false;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  private void ValidateSchema()
  {
    foreach (var t in _TableDefs.Values)
    {
      // Circular reference test.
      foreach (var rel in t.RelatedDataSets)
      {
        if (rel.HasTableDependency(t))
        {
          // This co-dependency only matters when we have enforced one->one relations.
          // Otherwise, one 
          string msg = $"A circular reference from table: {t.Name} to: {rel.TargetSet.Name} was detected!";
          throw new InvalidOperationException(msg);
        }
      }

      // Primary test.
      foreach (var pTable in t.RelatedDataSets)
      {
        // The parent table MUST have a primary key!
        bool hasPrimary = ReflectionTools.HasInterface<IHasPrimary>(pTable.TargetSet.DataType);
        if (!hasPrimary)
        {
          //// This might be a mapping table.  If it is we can consider it valid as type + member checks would have already happened!
          //if (!ReflectionTools.HasAttribute<MappingTableAttribute>(pTable.TargetSet.DataType))
          //{
          //  string msg = $"The data type: {pTable.TargetSet.DataType} is a parent of {t.DataType}, but does not implement interface: {nameof(IHasPrimary)} or have the '{nameof(MappingTableAttribute)}' set!";
          //  throw new InvalidOperationException(msg);
          //}

        }
      }
    }

  }

  // --------------------------------------------------------------------------------------------------------------------------
  internal void AddMappingSet(TableDef def)
  {
    // TOOD: Can the def be setup to denote that it is for mapping?
    this._TableDefs.Add(def.Name, def);
  }

  // --------------------------------------------------------------------------------------------------------------------------
  private void InitTableDef(string name, Type useType)
  {
    var def = new TableDef(useType, name, this);
    this.TypesToTableDef.Add(useType, def);
    _TableDefs.Add(name, def);
  }

  // --------------------------------------------------------------------------------------------------------------------------
  internal bool HasTableDef(string tableName, Type propertyType)
  {
    if (_TableDefs.TryGetValue(tableName, out TableDef? def))
    {
      return def.DataType == propertyType;
    }
    else
    {
      return false;
    }
  }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Returns the SQL that is required to represent this schema in a database.
  /// </summary>
  /// <remarks>
  /// For the moment, this only supports sqlite syntax.  More options (postgres) will be added later.
  /// </remarks>
  public string GetCreateSQL()
  {
    var sb = new StringBuilder(0x800);

    // Sort all tables by dependency.
    List<TableDef> defs = SortDependencies(_TableDefs);


    // For each of the defs, we have to build our queries.
    foreach (var d in defs)
    {
      string createTable = d.GetCreateQuery();
      sb.AppendLine(createTable);
    }

    return sb.ToString();



    // throw new NotImplementedException();
  }

  //// --------------------------------------------------------------------------------------------------------------------------
  ///// <summary>
  ///// NOTE: This should happen when we are building out our defs.
  ///// NOTE: It should also be part of the current sql flavor too!
  ///// </summary>
  //public static string FormatColumnName(string name)
  //{
  //  return name.ToLower();
  //}

  // --------------------------------------------------------------------------------------------------------------------------
  private List<TableDef> SortDependencies(Dictionary<string, TableDef> tableDefs)
  {
    var res = new List<TableDef>(tableDefs.Values.ToList());

    // LOL, this probably won't work!
    // It would be nice if it was just a matter of counting.  This will suffice for now.
    res.Sort((l, r) => l.RelatedDataSets.Count.CompareTo(r.RelatedDataSets.Count));

    return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Find + resolve mapping table for the given entity types.
  /// </summary>
  public TableDef? GetMappingTable<T1, T2>(bool allowNull = false)
  {
    var t1 = this.GetTableDef<T1>();
    var t2 = this.GetTableDef<T2>();

    string mtName = TableDef.ComputeMappingSetName(t1.Name, t2.Name);
    TableDef? res = this.GetTableDef(mtName);

    // Maybe the types are swapped....
    if (res == null)
    {
      mtName = TableDef.ComputeMappingSetName(t2.Name, t1.Name);
      res = this.GetTableDef(mtName);
    }

    if (res == null && !allowNull)
    {
      throw new NullReferenceException($"There is no mapping table with the name: {mtName} in this schema!");
    }

    return res;
  }

  // ------------------------------------------------------------------------------------------
  /// <summary>
  /// Generates a query to remove  entries from a mapping table.
  /// </summary>
  public string GetRemoveMappingQueryFor<TFor, TMapped>(List<int> forIds)
  {
    var forTable = GetTableDef<TFor>();

    var mtable = this.GetMappingTable<TFor, TMapped>();
    string forIdName = $"{forTable.Name}_{nameof(IHasPrimary.ID)}".ToLower();
    string idsList = $"({string.Join(",", forIds)})";

    string query = $"DELETE FROM {mtable.Name} WHERE {forIdName} IN {idsList}";
    return query;
  }
}

// ============================================================================================================================
/// <summary>
/// Describes a table that another is dependent upon.
/// This is your typical Foreign Key relationship in an RDBMS system.
/// </summary>
public class RelatedDatasetInfo
{
  public TableDef TargetSet { get; set; }
  public ERelationType RelationType { get; set; }

  /// <summary>
  /// The name of the property that contains the table in question.
  /// </summary>
  /// <value></value>
  /// <remarks>This only applies to child tables.</remarks>
  public string PropertyPath { get; set; } = string.Empty;

  public ColumnDef TargetIDColumn { get; set; } = null!;

  // public MappingTableAttribute? MappingTableData { get; set; } = null;

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// This tells us if we have a dependency on the given table, anywhere in the chain....
  /// </summary>
  internal bool HasTableDependency(TableDef t)
  {
    foreach (var dep in this.TargetSet.RelatedDataSets)
    {
      if (dep.TargetSet.DataType == t.DataType)
      {
        return true;
      }
      //if (dep.HasTableDependency(t))
      //{
      //  return true;
      //}
    }

    return false;
  }
}
