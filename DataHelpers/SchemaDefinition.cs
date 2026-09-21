using drewCo.Tools;
using System.Text;
using System.Collections;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Formats.Asn1;
using drewCo.Tools.Logging;
using System.Reflection;
using System.Net.WebSockets;

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
      // and association are all resolved by type.  Then the first pass of this resolver is made
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

    PopulateAssociations();

    PopulateIndexes();

    ValidateSchema();

    CreatePropertyMap();

  }

  // --------------------------------------------------------------------------------------------------------------------------
  private void PopulateIndexes()
  {
    foreach (var def in _TableDefs.Values)
    {
      // Now we can populate all of the members.
      def.PopulateIndexes();
    }
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
  /// <summary>
  /// Creates a QueryParameters instance based on the given object.
  /// The object must be an instance of a type defined in the schema.
  /// </summary>
  [Obsolete("Use 'ResolveQueryParams' or 'CreateParams'.  This version will be removed!")]
  public QueryParams ComputeParametersFor<T>(T obj)
  {
    var res = CreateParams(obj!);
    return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Gets the ID value for the given relationship, or null if it isn't set.
  /// </summary>
  private object? GetAssociationID(ColumnDef col, object? obj)
  {
    var relInstance = col.AssociationDef!.TargetProperty!.GetValue(obj) as ISingleAssociation;
    if (relInstance == null) { return null; }

    int res = relInstance.ID;
    if (res == 0)
    {
      return null;
    }
    return res;
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
  private void PopulateAssociations()
  {
    foreach (var def in _TableDefs.Values)
    {
      // Now we can populate all of the members.
      def.PopulateAssociations();
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
      var generatedSets = def.PopulateAssociationMembers();
      allGeneratedSets.AddRange(generatedSets);
    }

    // Now we can remove all of the temp, related columns from each of the sets:
    foreach (var def in _TableDefs.Values)
    {
      var toRemove = (from x in def.Columns where x.DataType == ColumnDef.ASSOCIATION_PLACEHOLDER select x).ToList();
      foreach (var item in toRemove)
      {
        def.RemoveCol(item);
      }
    }

    // Add the generated sets to the Schema def.
    // We filter them first because at time of writing it is possible to double-define them (no way to detect that one has been created in prior step)
    allGeneratedSets = GetUniqueSets(allGeneratedSets);


    foreach (var item in allGeneratedSets)
    {
      this._TableDefs.Add(item.Name, item);
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

  //// --------------------------------------------------------------------------------------------------------------------------
  //internal void AddMappingSet(TableDef def)
  //{
  //  // TOOD: Can the def be setup to denote that it is for mapping?
  //  this._TableDefs.Add(def.Name, def);
  //}

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

  // --------------------------------------------------------------------------------------------------------------------------
  private List<TableDef> SortDependencies(Dictionary<string, TableDef> tableDefs)
  {
    var candidates = new List<TableDef>(tableDefs.Values.ToList());

    // All tables with no associations go at the top.
    // Then we can do them one by one...
    // NOTE: There is certainly a way better way to do this, but we will live with it for now....
    var used = new HashSet<TableDef>();
    var res = new List<TableDef>();

    // NOTE: This can be folded in to the main loop...
    var zeroDeps = (from x in candidates where x.RelatedDataSets.Count == 0 select x).ToList();
    foreach (var item in zeroDeps)
    {
      candidates.Remove(item);
      used.Add(item);
      res.Add(item);
    }

    while (candidates.Count > 0)
    {
      var nextBatch = new List<TableDef>();
      foreach (var item in candidates)
      {
        bool hasAll = true;
        foreach (var rel in item.RelatedDataSets)
        {
          if (!used.Contains(rel.TargetSet))
          {
            hasAll = false;
            break;
          }
        }
        if (hasAll)
        {
          nextBatch.Add(item);
        }
      }

      // Remove identified items from the list of candidates.
      if (nextBatch.Count == 0) { throw new Exception("something went wrong!"); }
      foreach (var item in nextBatch)
      {
        used.Add(item);
        candidates.Remove(item);
        res.Add(item);
      }
      nextBatch.Clear();
    }


    // LOL, this probably won't work!
    // It would be nice if it was just a matter of counting.  This will suffice for now.
    // candidates.Sort((l, r) => l.RelatedDataSets.Count.CompareTo(r.RelatedDataSets.Count));
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

  // ------------------------------------------------------------------------------------------
  /// <summary>
  /// Get a query clause that will allow for a sub-selection of the data.
  /// Useful for pagination.
  /// </summary>
  public string GetPaginationClause(int pageNumber, int pageSize)
  {
    // This will work for both postgres and sqlite I believe...
    string res = $"LIMIT {pageSize} OFFSET {(pageNumber - 1) * pageSize}";

    return res;
  }

  // ------------------------------------------------------------------------------------------
  public string GetCountQuery<T>(string? criteria = null)
  {
    // This should work for all flavors....
    var td = GetTableDef<T>();
    string res = $"SELECT COUNT(*) FROM {td.Name}";
    if (criteria != null)
    {
      if (!criteria.StartsWith("where", StringComparison.OrdinalIgnoreCase))
      {
        criteria = "WHERE " + criteria;
      }
      res += " " + criteria;
    }
    return res;
  }


  // --------------------------------------------------------------------------------------------------------------------------
  public QueryParams? ResolveQueryParams(object? qParams)
  {
    QueryParams? useParams = null;
    if (qParams != null)
    {
      if (qParams is QueryParams)
      {
        useParams = qParams as QueryParams;
      }
      else
      {
        useParams = CreateParams(qParams);
      }
    }

    return useParams;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Create a set of dynamic query parameters from the given object.
  /// This allows us to use some of our conventions for mapping relationships to types.
  /// </summary>
  QueryParams CreateParams(object fromInstance, bool includeNulls = false, bool includeID = false)
  {
    if (fromInstance == null) { throw new ArgumentNullException($"Please provide an instance for {nameof(fromInstance)}"); }
    Type t = fromInstance.GetType();

    // throw new InvalidOperationException("make sure to use correct property names for relation mappings!  NOT the type names!  Use 'CanModelOneToManyRelationship' as a starting point!");

    var qpb = new QueryParamsBuilder(this.Flavor);

    var td = GetTableDef(t, true);
    if (td == null)
    {
      BuildParamsFromNonTableType(t, fromInstance, includeID, includeNulls, qpb);
    }
    else
    {

      // Build the params from a known table table.
      foreach (var col in td.Columns)
      {
        var prop = col.PropInfo;

        if (col.IsPrimary && !includeID) { continue; }
        if (col.AssociationDef != null)
        {
          var ad = col.AssociationDef;
          switch (ad.AssociationType)
          {
            case EAssociationType.Single:

              string setName = ad.DataSetName;
              string useName = col.DataStoreName; 

              // TODO: I think I can get this directly from the def...or at least I should be able to.
              var relType = col.AssociatedDataSet.TargetSet.DataType; // col.PropInfo.PropertyType.GetGenericArguments()[0];
              var relVal = ad.TargetProperty.GetValue(fromInstance);
              if (relVal == null || (relVal as ISingleAssociation).ID == 0)
              {
                // This is null, or unset:
                if (includeNulls)
                {
                  qpb.Add(useName, null, typeof(int));
                }
                continue;
              }


              int useId = (relVal as ISingleAssociation).ID;
              qpb.Add(useName, useId, typeof(int));

              break;

            case EAssociationType.Many:

              // TODO: Decide what to do about this.  In this case, there could be many related instances
              // each with their own ID, etc.....
              // Scenario one:
              // A one -> many relationship just means that some other Dataset has an FK to this one.
              // In that case, there is nothing for us to include, esp. if this is an INSERT query.
              // TODO: We don't have any indication as to what type of query we are creating params for,
              // so we should look into it at some point.
              var manyVal = col.PropInfo.GetValue(fromInstance);
              if (manyVal == null)
              {
                // There is no data anyway, so we can skip.
                continue;
              }
              Log.Warning("There is currently no support for many relations!");
              continue;

            default:
              throw new InvalidOperationException("Unknown association type!");
          }
        }
        else
        {
          // Normal, non association column.

          object? useVal = prop.GetValue(fromInstance);

          // NOTE: Why does the value work for sqlite....
          if (useVal != null && prop.PropertyType.IsEnum)
          {
            if (useVal.GetType() == typeof(string))
            {
              useVal = Enum.Parse(prop.PropertyType, (string)useVal);
            }
            else
            {
              useVal = (int)useVal;
            }
          }

          if (!includeNulls && useVal == null)
          {
            // NOTE: Depending on what we are doing, and what data set / type we are targeting, we may
            // want to flag non-nullable values.  Requires more machinery, but might be nice....
            continue;
          }

          bool isComposite = ReflectionTools.HasInterface<ICompositeSerializer>(prop.PropertyType);
          if (isComposite)
          {
            // var cs = useVal as ICompositeSerializer;
            // var genFunc = typeof(ICompositeSerializer<>).MakeGenericType(new[] { cs.GetCompositeType() }).GetMethod("To");
            useVal = (useVal as ICompositeSerializer).Serialize(); // (string)genFunc.Invoke(useVal, null);

            // useVal = cs.ToS
          }
          qpb.Add(prop.Name, useVal, prop.PropertyType);
          // res.Add(prop.Name, new QueryParamValue(useVal, ToDbType(prop.PropertyType)));

        }
      }

    }


    var res = qpb.Build();
    return res;

  }

  private void BuildParamsFromNonTableType(Type t, object fromInstance, bool includeID, bool includeNulls, QueryParamsBuilder qpb)
  {
    var props = ReflectionTools.GetProperties(t);
    foreach (var prop in props)
    {
      // Don't attempt to include ids.
      if (prop.Name == nameof(IHasPrimary.ID) && !includeID) { continue; }
      if (ReflectionTools.HasAttribute<IgnoreAttribute>(prop)) { continue; }

      var relAttr = ReflectionTools.GetAttribute<AssociationAttribute>(prop);
      if (relAttr != null)
      {
        throw new InvalidOperationException("These should not have associations!");
        if (ReflectionTools.HasInterface<ISingleAssociation>(prop.PropertyType))
        {
          string setName = relAttr.DataSetName;
          string useName = relAttr.LocalIDPropertyName ?? setName + "_" + nameof(IHasPrimary.ID);

          var relType = prop.PropertyType.GetGenericArguments()[0];
          var relVal = prop.GetValue(fromInstance);
          if (relVal == null || (relVal as ISingleAssociation).ID == 0)
          {
            // TODO: If the property isn't nullable, we should raise a flag here!
            // Not sure if we should blow it up, but I will for now....
            // NOTE: This call isn't detecting the nullability of the type correctly!
            //if (!TableDef.IsNullableEx(item)) { 
            //  throw new Exception("The value for a non-nullable property is currently null!");
            //}

            // This is null, or unset:
            if (includeNulls)
            {
              qpb.Add(useName, null, typeof(int));
              // res.Add(useName, new QueryParamValue(null, ToDbType(typeof(int))));
            }
            continue;
          }


          int useId = (relVal as ISingleAssociation).ID;
          qpb.Add(useName, useId, typeof(int));
          // res.Add(useName, new QueryParamValue(useId, ToDbType(typeof(int))));
        }
        else if (ReflectionTools.HasInterface<IManyAssociation>(prop.PropertyType))
        {
          // TODO: Decide what to do about this.  In this case, there could be many related instances
          // each with their own ID, etc.....

          // Scenario one:
          // A one -> many relationship just means that some other Dataset has an FK to this one.
          // In that case, there is nothing for us to include, esp. if this is an INSERT query.
          // TODO: We don't have any indication as to what type of query we are creating params for,
          // so we should look into it at some point.
          var manyVal = prop.GetValue(fromInstance);
          if (manyVal == null)
          {
            // There is no data anyway, so we can skip.
            continue;
          }
          Log.Warning("There is currently no support for many relations!");
          continue;
        }
        else
        {
          throw new InvalidOperationException($"All associations should be represented with a {nameof(ISingleAssociation)} OR {nameof(IManyAssociation)} instance!");
        }

      }
      else
      {
        object? useVal = prop.GetValue(fromInstance);

        // NOTE: Why does the value work for sqlite....
        if (useVal != null && prop.PropertyType.IsEnum)
        {
          if (useVal.GetType() == typeof(string))
          {
            useVal = Enum.Parse(prop.PropertyType, (string)useVal);
          }
          else
          {
            useVal = (int)useVal;
          }
        }

        if (!includeNulls && useVal == null)
        {
          // NOTE: Depending on what we are doing, and what data set / type we are targeting, we may
          // want to flag non-nullable values.  Requires more machinery, but might be nice....
          continue;
        }

        bool isComposite = ReflectionTools.HasInterface<ICompositeSerializer>(prop.PropertyType);
        if (isComposite)
        {
          // var cs = useVal as ICompositeSerializer;
          // var genFunc = typeof(ICompositeSerializer<>).MakeGenericType(new[] { cs.GetCompositeType() }).GetMethod("To");
          useVal = (useVal as ICompositeSerializer).Serialize(); // (string)genFunc.Invoke(useVal, null);

          // useVal = cs.ToS
        }
        qpb.Add(prop.Name, useVal, prop.PropertyType);
        // res.Add(prop.Name, new QueryParamValue(useVal, ToDbType(prop.PropertyType)));
      }
    }
  }

}
