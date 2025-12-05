using drewCo.Tools;
using System.Text;
using System.Collections;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Linq.Expressions;
using drewCo.Tools.Logging;
using System.Reflection;

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
    foreach (var td in this.TableDefs)
    {
      td.CreatePropertyMap();
    }
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
}


// ============================================================================================================================
public class TableDef
{
  public Type DataType { get; private set; }

  /// <summary>
  /// The name of the table in the database.
  /// </summary>
  public string Name { get; private set; }
  public SchemaDefinition Schema { get; private set; }

  public List<RelatedDatasetInfo> RelatedDataSets { get; set; }

  // NOTE: This should probably be a dictionary.....
  private List<ColumnDef> _Columns = new List<ColumnDef>();
  public ReadOnlyCollection<ColumnDef> Columns { get { return new ReadOnlyCollection<ColumnDef>(_Columns); } }


  // --------------------------------------------------------------------------------------------------------------------------
  public TableDef(Type type_, string name_, SchemaDefinition schema_)
  {
    DataType = type_;
    Name = name_;
    Schema = schema_;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  // TODO: An indexer for the column name would be more better.
  /// <summary>
  /// Return the ColumnDef with the corresponding name, or null if it doesn't exist.
  /// </summary>
  public ColumnDef? GetColumn(string name)
  {
    var res = (from x in _Columns
               where x.PropertyName == name
               select x).FirstOrDefault();
    return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public ColumnDef? GetColumnByDataStoreName(string name)
  {
    var res = (from x in _Columns
               where x.DataStoreName == name
               select x).FirstOrDefault();
    return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public override int GetHashCode()
  {
    return Name.GetHashCode();
  }

  // --------------------------------------------------------------------------------------------------------------------------
  internal void PopulateMembers()
  {
    var allProps = ReflectionTools.GetProperties(DataType);
    foreach (var p in allProps)
    {
      if (!p.CanWrite) { continue; }
      if (ReflectionTools.HasAttribute<IgnoreAttribute>(p)) { continue; }

      // OPTIONS:
      const bool USE_JSON_IGNORE = true;
      if (USE_JSON_IGNORE)
      {
        if (ReflectionTools.HasAttribute<JsonIgnoreAttribute>(p))
        {
          continue;
        }
      }

      bool isUnique = ReflectionTools.HasAttribute<UniqueAttribute>(p);

      // TODO: The property type should also be checked for nullable!
      // TODO: ReflectionTools needs to be updated to include all of this so that nullables can be correctly detected!
      bool isNullable = ReflectionTools.HasAttribute<IsNullableAttribute>(p) ||
                        ReflectionTools.HasAttribute<System.Runtime.CompilerServices.NullableAttribute>(p) ||
                        p.PropertyType.Name.StartsWith("Nullable`1");

      bool isPrimary = p.Name == nameof(IHasPrimary.ID) || ReflectionTools.HasAttribute<PrimaryKey>(p);

      // NOTE: TODO: We should be assigning the relation type here!
      var relAttr = ReflectionTools.GetAttribute<RelationAttribute>(p);
      if (relAttr != null)
      {
        relAttr.RelationType = GetRelationType(p.PropertyType);
        relAttr.TargetProperty = p;
      }

      string colName = this.Schema.Flavor.GetDataStoreName(p.Name);

      // This is a normal property.
      // NOTE: Non-related lists can't be represented.... should we make it so that lists are always included?
      _Columns.Add(new ColumnDef(p.Name,
                                 colName,
                                 p.PropertyType,
                                 Schema.Flavor.TypeResolver.GetDataTypeName(p.PropertyType, isPrimary),
                                 isPrimary,
                                 isUnique,
                                 isNullable,
                                 relAttr,
                                 p));

    }
  }

  // --------------------------------------------------------------------------------------------------------------------------
  [Obsolete("Use IsNullable from drewco.tools.reflectiontools > 1.4.1.0")]
  public static bool IsNullableEx(Type t)
  {

    bool isNullable = ReflectionTools.HasAttribute<IsNullableAttribute>(t) ||
                      ReflectionTools.HasAttribute<System.Runtime.CompilerServices.NullableAttribute>(t) ||
                      t.Name.StartsWith("Nullable`1");

    return isNullable;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  [Obsolete("Use IsNullable from drewco.tools.reflectiontools > 1.4.1.0")]
  public static bool IsNullableEx(PropertyInfo p)
  {
    bool res = IsNullableEx(p.PropertyType);
    return res;
  }


  // --------------------------------------------------------------------------------------------------------------------------
  // TODO: This should be moved to ReflectionTools ASAP.
  [Obsolete("Use version from drewco.tools.reflectiontools > 1.4.1.0")]
  public static bool HasProperty(Type dataType, string propName, Type propType)
  {
    var props = ReflectionTools.GetProperties(dataType);
    var match = from x in props
                where x.Name == propName && x.PropertyType == propType
                select x;

    bool res = match.Count() == 1;
    return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  // NOTE: This is technically flavor specific as it is a query.  We will have to move the code at some point.....
  // TODO: This should be part of the ISQlFlavor code.
  public string GetCreateQuery()
  {
    var sb = new StringBuilder(0x400);
    sb.AppendLine($"CREATE TABLE IF NOT EXISTS {Name} (");

    var colDefs = new List<string>();
    var fkDefs = new List<string>();

    foreach (var col in Columns)
    {
      string useName = col.DataStoreName;

      string def = $"{useName} {col.DataType}";
      if (col.IsPrimary)
      {
        def += " PRIMARY KEY"; // + Schema.Flavor.GetIdentitySyntax(col); // flavo  PRIMARY KEY";
      }

      if (!col.IsNullable)
      {
        def += " NOT NULL";
      }
      else
      {
        def += " NULL";
      }

      if (col.IsUnique)
      {
        def += " UNIQUE";
      }

      colDefs.Add(def);

      if (col.RelatedDataSet != null)
      {

        string fk = $"FOREIGN KEY({useName}) REFERENCES {col.RelatedDataSet.TargetSet.Name}({col.RelatedDataSet.TargetIDColumn.PropertyName})";
        fkDefs.Add(fk);
      }

    }
    sb.AppendLine(string.Join(", " + Environment.NewLine, colDefs) + (fkDefs.Count > 0 ? "," : ""));

    foreach (var fk in fkDefs)
    {
      sb.AppendLine(fk);
    }


    sb.AppendLine(");");

    string res = sb.ToString();
    return res;


  }

  // --------------------------------------------------------------------------------------------------------------------------
  internal NamesAndValues GetNamesAndValues()
  {
    return GetNamesAndValues(this.Columns);
  }

  // --------------------------------------------------------------------------------------------------------------------------
  internal NamesAndValues GetNamesAndValues(IEnumerable<ColumnDef> columns, IEnumerable<string>? useCols = null)
  {
    var colNames = new List<string>();
    var colVals = new List<string>();
    string? pkName = null;

    ICollection<ColumnDef> useDefs = useCols != null ? SelectColumns(useCols) : this.Columns;
    foreach (var c in useDefs)
    {
      string colName = c.DataStoreName;

      if (c.IsPrimary)
      {
        pkName = colName;
        continue;
      }

      // NOTE: This makes no consideration for foreign keys, cols with defaults, etc.
      // We just throw them all in.
      colNames.Add(colName);
      colVals.Add("@" + c.PropertyName);  // NOTE: We are using the same casing as the original datatype for the value parameters!
    }

    return new NamesAndValues(colNames, colVals, pkName);
  }


  // --------------------------------------------------------------------------------------------------------------------------
  private ICollection<ColumnDef> SelectColumns(IEnumerable<string> useCols)
  {
    // NOTE: This is not efficient, but will do for now.
    var res = new List<ColumnDef>();
    foreach (var c in useCols)
    {
      foreach (var colDef in this._Columns)
      {
        if (c == colDef.PropertyName)
        {
          res.Add(colDef);
        }
      }
    }
    return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  internal string GetInsertPart()
  {
    var nv = GetNamesAndValues(this.Columns);
    string res = GetInsertPart(nv);
    return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  internal string GetInsertPart(NamesAndValues namesAndVals)
  {
    StringBuilder sb = new StringBuilder(0x400);
    sb.Append($"INSERT INTO {this.Name} (");
    sb.Append(string.Join(",", namesAndVals.ColNames));
    sb.Append(")");

    string res = sb.ToString();
    return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  internal string GetSelectByIDQuery()
  {
    NamesAndValues namesAndVals = GetNamesAndValues(this.Columns);

    var sb = new StringBuilder(0x400);
    sb.Append($"SELECT * FROM {this.Name} WHERE {namesAndVals.PrimaryKeyName} = @{nameof(IHasPrimary.ID)}");

    string res = sb.ToString();
    return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public string GetInsertQuery(string[]? useCols = null, bool returnId = true)
  {
    NamesAndValues namesAndVals = GetNamesAndValues(this.Columns, useCols);

    StringBuilder sb = new StringBuilder(0x400);
    string insertPart = GetInsertPart(namesAndVals);
    sb.Append(insertPart);

    sb.Append(" VALUES (");
    sb.Append(string.Join(",", namesAndVals.ColValues));

    sb.Append(")");

    // OPTIONS:
    const bool RETURN_ID = true;
    if (RETURN_ID && namesAndVals.PrimaryKeyName != null || returnId)
    {
      sb.Append($" RETURNING {namesAndVals.PrimaryKeyName ?? nameof(IHasPrimary.ID)}");
    }

    string res = sb.ToString();
    return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public string GetUpdateQuery()
  {
    var useCols = (from x in this.Columns
                   where x.RelatedDataSet == null
                   select x);
    var namesAndVals = GetNamesAndValues(useCols);

    var sb = new StringBuilder(0x400);
    var zipped = namesAndVals.ColNames.Zip(namesAndVals.ColValues, (a, b) => $"{a} = {b}");
    string assignments = string.Join(",", zipped);

    sb.Append($"UPDATE {this.Name} SET {assignments} WHERE {namesAndVals.PrimaryKeyName} = @{nameof(IHasPrimary.ID)}");

    string res = sb.ToString();
    return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  private Dictionary<Type, string[]> TypesToPropNames = new Dictionary<Type, string[]>();
  internal string GetSelectByExampleQuery(object example, IList<string>? props = null)
  {
    var t = example.GetType();
    if (!TypesToPropNames.TryGetValue(t, out string[] names))
    {
      names = (from x in ReflectionTools.GetProperties(t)
               select x.Name).ToArray();

      // TODO: Make sure that the names are actually correct?

      TypesToPropNames.Add(t, names);
    }

    string useCols = props == null ? "*" : string.Join(",", props);

    var sb = new StringBuilder(0x400);
    sb.Append($"SELECT {useCols} FROM {this.Name} ");

    int len = names.Length;
    sb.Append($"WHERE {names[0]} = @{names[0]}");
    for (int i = 0; i < len; i++)
    {
      sb.Append($" AND {names[i]} = @{names[i]}");
    }

    string res = sb.ToString();
    return res;

  }

  // TODO: We should be able to use a non-generic select query for mapping tables.....
  // ---------------------------------------------------------------------------------------------------
  // NOTE: I think that the 'TableDef' type should be a generic.....
  /// <summary>
  /// Create a select query using the named properties.  If null, all properties (*) will be used.
  /// </summary>
  public string GetSelectQuery<T>(IEnumerable<Expression<Func<T, object>>>? toSelect = null, Expression<Func<T, bool>>? predicate = null)
  {
    var t = typeof(T);

    var colNames = toSelect == null ? [] : (from x in toSelect
                                            select ReflectionTools.GetPropertyName(x)).ToArray();

    var sb = new StringBuilder();
    string cols = colNames.Length == 0 ? "*" : string.Join(",", colNames);

    sb.Append($"SELECT {cols} FROM {this.Name}");

    if (predicate != null)
    {
      throw new InvalidOperationException("the where generator is bad code!");
      string whereClause = WhereBuilder.ToSqlWhere(predicate);
      sb.Append(" WHERE ");
      sb.Append(whereClause);
    }

    string res = sb.ToString();
    return res;
  }

  // ---------------------------------------------------------------------------------------------------
  /// <summary>
  /// Add any required members to the def based on its relationships.
  /// </summary>
  internal void PopulateRelationships()
  {
    RelatedDataSets = new List<RelatedDatasetInfo>();

    foreach (var col in Columns)
    {
      if (col.RelationDef != null)
      {
        var targetSet = Schema.GetTableDef(col.RelationDef.DataSetName);

        RelatedDatasetInfo ddsInfo = new()
        {
          TargetSet = targetSet,
          TargetIDColumn = targetSet.GetColumn(nameof(IHasPrimary.ID)),
          PropertyPath = col.PropertyName,
          RelationType = col.RelationDef.RelationType,
        };
        this.RelatedDataSets.Add(ddsInfo);
        col.RelatedDataSet = ddsInfo;
      }
    }
  }

  // --------------------------------------------------------------------------------------------------------------------------
  private ERelationType GetRelationType(Type t)
  {
    if (ReflectionTools.HasInterface<ISingleRelation>(t))
    {
      return ERelationType.Single;
    }

    if (ReflectionTools.HasInterface<IManyRelation>(t))
    {
      return ERelationType.Many;
    }

    throw new InvalidOperationException($"The type: {t} is not a valid relation type!");
  }

  // --------------------------------------------------------------------------------------------------------------------------
  private void AddColumn(ColumnDef colDef)
  {
    // Only add the def if it doesn't already have 
    var match = (from x in _Columns where x.PropertyName == colDef.PropertyName select x).FirstOrDefault();
    if (match != null)
    {
      if (ColumnDef.AreSame(colDef, match))
      {
        Log.Verbose("A duplicate column def named: {colDef.Name} already exists, and will be overwritten!");
        _Columns.Remove(match);
      }
    }

    // We can also check the attributes that might indicate the associated column (later)?


    _Columns.Add(colDef);
  }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// This will create any column defs that are required to correctly represent the relations
  /// in the schema.  This step DOES not create the actual links, it just gets the dynamic members in place
  /// so that they can be validated + populated correctly in a later step.
  /// </summary>
  /// <returns>
  /// A list of new <see cref="TableDef"/> instances that represent mapping sets that should be created.
  /// These mapping sets are auto-generated based on how relations are setup in the rest of the schema.
  /// </returns>
  internal List<TableDef> PopulateRelationMembers()
  {
    var newMappingSets = new List<TableDef>();

    var toAdd = new List<ColumnDef>();

    // Let's find all the relations first...
    foreach (var col in this.Columns)
    {
      var rd = col.RelationDef;
      if (rd != null)
      {
        // OBSOLETE:??
        // rd.RelationType = GetRelationType(col.RuntimeType);

        // Get the matching data set....
        var targetSet = this.Schema.GetTableDef(col.RelationDef.DataSetName);
        if (targetSet == null)
        {
          throw new InvalidOperationException($"There is no data set named: {targetSet}");
        }

        // This is where we can setup the link to the other data set....
        // Depends on the type of relation, of course.
        if (ReflectionTools.HasInterface<ISingleRelation>(col.RuntimeType))
        {
          // In single relations we use a column from this type.
          // Because we are using one of our special data types, that defined column is mapped to it. <-- review this, does it make sense?
          string propName = rd.LocalIDPropertyName ?? $"{rd.DataSetName}_{nameof(IHasPrimary.ID)}";
          string colName = Schema.Flavor.GetDataStoreName(propName);
          rd.LocalIDPropertyName = propName;
          // rd.RelationType = GetRelationType(col);
          var match = this.GetColumn(propName);
          if (match == null) { match = (from x in toAdd where x.PropertyName == propName select x).SingleOrDefault(); }

          if (match == null)
          {
            // Create the new def.....
            string dbTypeName = Schema.Flavor.TypeResolver.GetDataTypeName(typeof(int), false);
            var cd = new ColumnDef(propName, colName, typeof(int), dbTypeName, false, col.IsUnique, col.IsNullable, rd, null);
            toAdd.Add(cd);
          }
        }
        else if (ReflectionTools.HasInterface<IManyRelation>(col.RuntimeType))
        {
          // If the target dataset also has a many relationship that points back to this one, then
          // we probably need to create some kind of mapping table!
          var relSet = this.Schema.GetTableDef(col.RelationDef.DataSetName);
          if (relSet == null)
          {
            throw new InvalidOperationException($"Related data set does not exist in the schema!");
          }

          // Find a ref to this dataset in the def?
          var mutualRelation = relSet.GetRelationTo(this);

          // if (mutualRelation.DataSetName == this.Name) { throw new Exception("cicular dependency?"); }

          if (mutualRelation != null &&
              mutualRelation.RelationType == ERelationType.Many &&
              mutualRelation.DataSetName == this.Name)
          {
            // This is a many-many relationship!
            string mtName = ComputeMappingSetName(relSet, mutualRelation);
            var matchSet = Schema.GetTableDef(mtName, true);
            if (matchSet == null)
            {
              TableDef td = CreateMappingSet(relSet, mutualRelation, mtName);

              // Make sure that it doesn't already exist!
              var existing = (from x in newMappingSets
                              where x.Name == td.Name
                              select x).SingleOrDefault();
              if (existing != null)
              {
                throw new InvalidOperationException("The mapping table may already exists.... check it more....");
              }
              newMappingSets.Add(td);

            }
            else
            {
              // Handle the scenario where the mapping table is already defined....
              throw new Exception("Handle this scenario: see comment!");
            }
          }
          else
          {
            // This is a one-many relationship.

            // This columnd def gets added to the target dataset, NOT this one....
            string useName = rd.TargetIDPropertyName ?? $"{this.Name}_{nameof(IHasPrimary.ID)}";
            string sqlName = Schema.Flavor.GetDataStoreName(useName);

            string dbTypeName = Schema.Flavor.TypeResolver.GetDataTypeName(typeof(int), false);

            var useRelation = new RelationAttribute(this.Name);
            useRelation.TargetIDPropertyName = nameof(IHasPrimary.ID);
            useRelation.RelationType = ERelationType.Many;
            useRelation.TargetProperty = mutualRelation.TargetProperty;

            // TODO: This is where we would check to make sure that there is already a column with the correct
            // name that corresponds to a SingleRelation or ManyRelation member....
            var match = targetSet.GetColumn(useName);
            if (match != null)
            {
              // There should be a relation, and it should point to this set!
              if (match.RelationDef == null || match.RelationDef.DataSetName != this.Name)
              {
                throw new InvalidOperationException($"There should be a relation on column: {useName} that points to this Dataset ({this.Name})!");
              }
              int x = 10;
            }

            var colDef = new ColumnDef(useName, sqlName, typeof(int), dbTypeName, false, false, false, useRelation, null);
            targetSet.AddColumn(colDef);
          }
        }
        else if (col.RuntimeType == typeof(int))
        {
          // This is probably a generated property... I think that we can maybe leave it alone / ignore it.
          // We don't need to create a new column, table, etc.
          // Otherwise we need to flag the column as 'generated' which may be the best approach....
          continue;
        }
        else
        {
          throw new InvalidOperationException($"The column type: {col.RuntimeType} is not supported!");
        }
      }
    }

    foreach (var newCol in toAdd)
    {
      this.AddColumn(newCol);
    }

    return newMappingSets;
  }


  // --------------------------------------------------------------------------------------------------------------------------
  internal static string ComputeMappingSetName(string name1, string name2)
  {
    // We use sorted names so that the mapping set name is always the same for two given sets.
    // NOTE: A direct comparison of the names will execute more faster.
    var names = (new[] { name1, name2 }).Order().ToArray();
    return $"{names[0]}_to_{names[1]}_map";
  }

  // --------------------------------------------------------------------------------------------------------------------------
  internal static string ComputeMappingSetName(TableDef relSet, RelationAttribute mutualRelation)
  {
    return ComputeMappingSetName(relSet.Name, mutualRelation.DataSetName);
  }


  // --------------------------------------------------------------------------------------------------------------------------
  private TableDef CreateMappingSet(TableDef relSet, RelationAttribute mutualRelation, string mtName)
  {
    Log.Verbose($"Many -> Many relation detected.  A mapping dataset will be created!");
    Log.Verbose($"Mapping dataset name: {mtName}");

    Type intType = typeof(int);
    string intTypeName = Schema.Flavor.TypeResolver.GetDataTypeName(typeof(int), false);

    string useName = nameof(IHasPrimary.ID);
    string sqlName = Schema.Flavor.GetDataStoreName(useName);

    var td = new TableDef(null, mtName, this.Schema);
    td.AddColumn(new ColumnDef(useName, sqlName, intType, intTypeName, true, false, false, null, null));

    // Now the id refs for each of the data sets.
    string idCol1 = $"{relSet.Name}_{nameof(IHasPrimary.ID)}";
    string idCol2 = $"{mutualRelation.DataSetName}_{nameof(IHasPrimary.ID)}";
    var cols = new[] { idCol1, idCol2 };

    int index = 0;
    foreach (var c in cols)
    {
      sqlName = Schema.Flavor.GetDataStoreName(c);
      var cd = new ColumnDef(c, sqlName, intType, intTypeName, false, false, false, new RelationAttribute()
      {
        DataSetName = index == 0 ? relSet.Name : mutualRelation.DataSetName,
        LocalIDPropertyName = c,
        RelationType = ERelationType.Single
      }, null);
      td.AddColumn(cd);
      ++index;
    }

    return td;
  }

  // ------------------------------------------------------------------------------------------------
  private RelationAttribute? GetRelationTo(TableDef dataset)
  {

    foreach (var item in this.Columns)
    {
      if (item.RelationDef != null && item.RelationDef.DataSetName == dataset.Name)
      {
        return item.RelationDef;
      }
    }
    return null;
  }

  // ------------------------------------------------------------------------------------------------
  /// <summary>
  /// Tells us if any of the members of this dataset have a relation to the given set.
  /// </summary>
  private bool HasRelationTo(TableDef dataset)
  {
    RelationAttribute? relAttr = GetRelationTo(dataset);
    return relAttr != null;
  }

  // ------------------------------------------------------------------------------------------------
  /// <summary>
  /// This is only used during schema generation so we can remove temp columns (@_RELATION)
  /// </summary>
  internal void RemoveCol(ColumnDef colDef)
  {
    this._Columns.Remove(colDef);
  }

  public PropMap PropMap { get; private set; } = null!;

  // --------------------------------------------------------------------------------------------------------------------------
  internal void CreatePropertyMap()
  {
    PropMap = new PropMap();

    foreach (var colDef in this.Columns)
    {

      // For the most part, only scalars get added to the property map!
      //if (colDef.PropInfo != null && ReflectionTools.IsSimpleType(colDef.RuntimeType))
      //{
      //  PropMap.Add(colDef.DataStoreName, colDef);
      //}
      // colDef.RuntimeType
    }

    int z = 23905;
  }


  //// ------------------------------------------------------------------------------------------------
  ///// <summary>
  ///// Creates an insert query using the given object as a model.
  ///// This function, in particular, will leave out optional/null params.
  ///// </summary>
  //public string GetInsertQueryFor<T>(T addr)
  //{
  //  var props = ReflectionTools.GetProperties<T>();
  //  var qParams = Helpers.CreateParams("insert", addr, true);

  //  foreach (var c in this.Columns)
  //  {
  //    if (qParams.TryGetValue(c.Name, out var value)) {
  //      if (value == null && c.IsNullable) { 
  //        // We won't include this, as it isn't needed!
  //      }
  //    }
  //  }

  //  //return "";
  //  // var map = from x in props select new {Key = x.Name,  Value = x.GetValue(addr)}).ToDictionary();
  //  throw new NotImplementedException();
  //}
}

public class PropMapInfo
{
}


// ============================================================================================================================
public class ColumnDef
{
  /// <summary>
  /// Special DataType name used for placeholder relation defs during schema generation.
  /// </summary>
  public const string RELATION_PLACEHOLDER = "@_RELATION";

  // --------------------------------------------------------------------------------------------------------------------------
  public string? PropertyName { get; private set; }                 // This is the same name as the property that this def comes from.
  public string DataStoreName { get; private set; }                 // The name that is used in the data-store (SQL for example)
  public Type RuntimeType { get; private set; }
  public string DataType { get; private set; }
  public bool IsPrimary { get; private set; }
  public bool IsUnique { get; private set; }
  public bool IsNullable { get; private set; }

  public PropertyInfo? PropInfo { get; private set; } = null;

  //// NOTE: This has a non-private setter b/c we have to update them sometimes, after the fact,
  //// because of the sloppy way that we are currently creating the table defs.
  //// we should have it so that the columns are added to the def BEFORE we attempt resolve the relationships.
  public RelatedDatasetInfo? RelatedDataSet { get; internal set; }

  /// <summary>
  /// The relationship that is defined for this column.
  /// This data is really only useful when the SchemaDefs are being computed.
  /// </summary>
  internal RelationAttribute? RelationDef { get; set; } = null;

  // --------------------------------------------------------------------------------------------------------------------------
  public ColumnDef(string propName, string dataStoreName, Type runtimeType, string dataType, bool isPrimary, bool isUnique, bool isNullable, RelationAttribute? relationDef_, PropertyInfo? propInfo_)
  {
    PropertyName = propName;
    DataStoreName = dataStoreName;
    RuntimeType = runtimeType;
    DataType = dataType;
    IsPrimary = isPrimary;
    IsUnique = isUnique;
    IsNullable = isNullable;
    RelationDef = relationDef_;
    PropInfo = propInfo_;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public static bool AreSame(ColumnDef colDef, ColumnDef match)
  {
    bool res = (colDef.PropertyName == match.PropertyName &&
                colDef.IsPrimary == match.IsPrimary &&
                colDef.DataType == match.DataType &&
                colDef.IsUnique == match.IsUnique);

    return res;
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
