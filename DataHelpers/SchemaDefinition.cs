using drewCo.Tools;
using System.Text;
using System.Collections;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Reflection;
using System.Linq.Expressions;
using System.Reflection.Emit;
using System.ComponentModel;
using drewCo.Tools.Logging;

//using System.com

namespace DataHelpers.Data;

internal record NamesAndValues(List<string> ColNames, List<string> ColValues, string? PrimaryKeyName);

// ============================================================================================================================
public class SchemaDefinition
{
  private object ResolveLock = new object();
  private Dictionary<string, TableDef> _TableDefs = new Dictionary<string, TableDef>(StringComparer.OrdinalIgnoreCase);
  private Dictionary<Type, TableDef> TypesToTableDef = new Dictionary<Type, TableDef>();
  public ReadOnlyCollection<TableDef> TableDefs { get { return new ReadOnlyCollection<TableDef>(_TableDefs.Values.ToList()); } }

  // --------------------------------------------------------------------------------------------------------------------------
  public TableDef? GetTableDef<T>(bool allowNull = false)
  {
    if (!TypesToTableDef.TryGetValue(typeof(T), out TableDef? res))
    {
      if (!allowNull)
      {
        throw new InvalidOperationException($"There is no table def for type: {typeof(T)} in this schema!");
      }
      return null;
    }
    return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public TableDef? GetTableDef(string name, bool allowNull = false)
  {
    var res = _TableDefs[name];
    if (res == null && !allowNull)
    {
      throw new InvalidOperationException($"There is no table named: {name} in this schema!");
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
    throw new InvalidOperationException("This function is currently not working and breaking some tests.  review its use + write some standalone test cases, please");

    var tableDef = GetTableDef<T>(false)!;
    object res = GetProxyObjectInstance(tableDef);
    PopulateProxyObject(res, child);
    return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Populate the given proxy object with the data from the given type.
  /// </summary>
  private void PopulateProxyObject<T>(object proxyData, T srcData)
  {
    // if (data == null)
    // {
    //     throw new ArgumentNullException(nameof(data));
    // }
    Type proxyType = proxyData.GetType();
    Type srcType = typeof(T);

    // NOTE: This kind of functionality could be an emitted function on our proxy type!
    TableDef def = GetTableDef<T>(false)!;
    foreach (var c in def.Columns)
    {
      // if (c.IsPrimary) { continue; }
      string memberName = c.Name;
      string propPath = c.Name;
      if (c.Relationship != null)
      {
        // This is where we have to figure out how to get our data into the proxy object...
        // Basically we would compute the nested property path.
        propPath = c.Relationship.PropertyName;
      }

      PropertyInfo proxyProp = proxyType.GetProperty(memberName);

      object srcVal = ReflectionTools.GetNestedPropertyValue(srcData, propPath);
      proxyProp.SetValue(proxyData, srcVal);

    }


    // throw new NotImplementedException();
  }

  // --------------------------------------------------------------------------------------------------------------------------
  private Dictionary<TableDef, Type> TableDefToInstanceTypes = new Dictionary<TableDef, Type>();
  private object GetProxyObjectInstance(TableDef tableDef)
  {
    if (tableDef == null)
    {
      throw new ArgumentNullException(nameof(tableDef));
    }

    Type? instanceType = null;
    if (!TableDefToInstanceTypes.TryGetValue(tableDef, out instanceType))
    {
      // We need to create that new type....
      // TODO: This is code to resolve an assembly/module buildre...
      AssemblyName aName = new AssemblyName("SchemaDefiniton_Types");
      AssemblyBuilder ab = AssemblyBuilder.DefineDynamicAssembly(aName, AssemblyBuilderAccess.Run);
      ModuleBuilder mb = ab.DefineDynamicModule(aName.Name);

      TypeBuilder tb = mb.DefineType(tableDef.Name + "_Proxy", TypeAttributes.Public);

      foreach (var c in tableDef.Columns)
      {
        // if (c.IsPrimary) { continue; }
        PropertyBuilder pb = tb.DefineProperty(c.Name,
                                               PropertyAttributes.HasDefault,
                                               CallingConventions.Standard,
                                               c.RuntimeType, null);

        FieldBuilder backer = tb.DefineField($"_{c.Name}", c.RuntimeType, FieldAttributes.Private);

        // The property set and property get methods require a special
        // set of attributes.
        MethodAttributes getSetAttr = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;

        // ********* GETTER **********************
        // Define the "get" accessor method.
        MethodBuilder getterBuilder = tb.DefineMethod($"get_{c.Name}", getSetAttr,
                                               c.RuntimeType, Type.EmptyTypes);
        ILGenerator getILGen = getterBuilder.GetILGenerator();
        getILGen.Emit(OpCodes.Ldarg_0);
        getILGen.Emit(OpCodes.Ldfld, backer);
        getILGen.Emit(OpCodes.Ret);


        // ************* SETTER *********************
        // Define the "set" accessor method.
        MethodBuilder setBuilder = tb.DefineMethod($"set_{c.Name}", getSetAttr,
                                                   null, new Type[] { c.RuntimeType });

        ILGenerator setILGen = setBuilder.GetILGenerator();

        setILGen.Emit(OpCodes.Ldarg_0);
        setILGen.Emit(OpCodes.Ldarg_1);
        setILGen.Emit(OpCodes.Stfld, backer);
        setILGen.Emit(OpCodes.Ret);

        // Last, we must map the two methods created above to our PropertyBuilder to
        // their corresponding behaviors, "get" and "set" respectively.
        pb.SetGetMethod(getterBuilder);
        pb.SetSetMethod(setBuilder);
      }

      instanceType = tb.CreateType();
      if (instanceType == null)
      {
        throw new InvalidOperationException($"Could not create a proxy type for Table Definition: {tableDef.Name}!");
      }

      TableDefToInstanceTypes.Add(tableDef, instanceType);
    }

    object? res = Activator.CreateInstance(instanceType);
    if (res == null)
    {
      throw new InvalidOperationException("Could not get an instance of proxy type!");
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
    // else
    // {
    //   throw new NotSupportedException($"The expression type is not supported!");
    // }

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
        values.Add($"@{c.Name}");
        //        }

        // NOTE: This is where we will check for nulls, default values, etc.
        columns.Add(c.Name);
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
  public ISqlFlavor Flavor { get; private set; }
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
  }

  // --------------------------------------------------------------------------------------------------------------------------
  private void ValidateSchema()
  {
    foreach (var t in _TableDefs.Values)
    {
      // Circular reference test.
      foreach (var dep in t.ParentSets)
      {
        if (dep.HasTableDependency(t))
        {
          string msg = $"A circular reference from table: {t.Name} to: {dep.TargetSet.Name} was detected!";
          throw new InvalidOperationException(msg);
        }
      }

      // Primary test.
      foreach (var pTable in t.ParentSets)
      {
        // The parent table MUST have a primary key!
        bool hasPrimary = ReflectionTools.HasInterface<IHasPrimary>(pTable.TargetSet.DataType);
        if (!hasPrimary)
        {
          string msg = $"The data type: {pTable.TargetSet.DataType} is a parent of {t.DataType}, but does not implement interface: {nameof(IHasPrimary)}";
          throw new InvalidOperationException(msg);
        }
      }
      // if (t.DependentTables.Count > 0 && !ReflectionTools.HasInterface<IHasPrimary>(t.DataType))
      // {
      // }
    }

  }

  // --------------------------------------------------------------------------------------------------------------------------
  private void InitTableDef(string name, Type useType)
  {
    var def = new TableDef(useType, name, this);
    this.TypesToTableDef.Add(useType, def);
    _TableDefs.Add(name, def);
  }

  // // --------------------------------------------------------------------------------------------------------------------------
  // public SchemaDefinition AddTable<T>()
  // {
  //   string name = typeof(T).Name;
  //   return AddTable<T>(name);
  // }

  // // --------------------------------------------------------------------------------------------------------------------------
  // public SchemaDefinition AddTable<T>(string tableName)
  // {
  //   ResolveTableDef(tableName, typeof(T));
  //   return this;
  // }

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


  //// --------------------------------------------------------------------------------------------------------------------------
  //[Obsolete("Do not use this at this time!")]
  //internal TableDef ResolveTableDef(string tableName, Type propertyType)
  //{
  //  lock (ResolveLock)
  //  {
  //    if (_TableDefs.TryGetValue(tableName, out TableDef def))
  //    {
  //      if (def.DataType != propertyType)
  //      {
  //        throw new InvalidOperationException($"There is already a table named '{tableName}' with the data type '{def.DataType}'");
  //      }
  //      return def;
  //    }
  //    else
  //    {
  //      Type useType = propertyType;
  //      bool isList = ReflectionTools.HasInterface<IList>(useType);
  //      if (isList)
  //      {
  //        useType = useType.GetGenericArguments()[0];
  //      }
  //      var res = new TableDef(useType, tableName, this);
  //      _TableDefs.Add(tableName, res);
  //      res.PopulateMembers();

  //      return res;
  //    }
  //  }
  //}

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
  /// <summary>
  /// NOTE: This should happen when we are building out our defs.
  /// NOTE: It should also be part of the current sql flavor too!
  /// </summary>
  public static string FormatName(string name)
  {
    return name.ToLower();
  }

  // --------------------------------------------------------------------------------------------------------------------------
  private List<TableDef> SortDependencies(Dictionary<string, TableDef> tableDefs)
  {
    var res = new List<TableDef>(tableDefs.Values.ToList());

    // LOL, this probably won't work!
    // It would be nice if it was just a matter of counting.  This will suffice for now.
    res.Sort((l, r) => l.ParentSets.Count.CompareTo(r.ParentSets.Count));

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

  public ReadOnlyCollection<DependentTable> ParentSets { get { return new ReadOnlyCollection<DependentTable>(_ParentSets); } }
  private List<DependentTable> _ParentSets = new List<DependentTable>();

  public ReadOnlyCollection<DependentTable> ChildSets { get { return new ReadOnlyCollection<DependentTable>(_ChildSets); } }
  private List<DependentTable> _ChildSets = new List<DependentTable>();

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
  public override int GetHashCode()
  {
    return Name.GetHashCode();
  }

  // --------------------------------------------------------------------------------------------------------------------------
  internal void PopulateMembers()
  {
    foreach (var p in ReflectionTools.GetProperties(DataType))
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
      bool isNullable = ReflectionTools.HasAttribute<IsNullableAttribute>(p) ||
                        ReflectionTools.HasAttribute<System.Runtime.CompilerServices.NullableAttribute>(p) ||
                        p.PropertyType.Name.StartsWith("Nullable`1");

      bool isPrimary = p.Name == nameof(IHasPrimary.ID) || ReflectionTools.HasAttribute<PrimaryKey>(p);

      var relAttr = ReflectionTools.GetAttribute<Relationship>(p);
      if (relAttr != null)
      {
        string setName = relAttr.DataSet ?? p.Name;
        var targetSet = Schema.GetTableDef(setName);

        // We need to know if this property is a single instance, or points to a list.
        // If it points to a list, we need to check the thing that it points to so we can
        // setup the correct 'many to many' tables + ids.
        // Note that the many-many tables only apply to SQL.  Other data stores can come up
        // with a better way to handle this data.
        if (ReflectionTools.HasInterface<IList>(p.PropertyType))
        {
          AddParentRelationship(p, isUnique, isNullable, targetSet);
        }
        else
        {
          AddChildRelationship(p, isUnique, isNullable, targetSet);
        }
      }
      else
      {
        // This is a normal property.
        // NOTE: Non-related lists can't be represented.... should we make it so that lists are always included?
        _Columns.Add(new ColumnDef(p.Name,
                                   p.PropertyType,
                                   Schema.Flavor.TypeResolver.GetDataTypeName(p.PropertyType, isPrimary),
                                   isPrimary,
                                   isUnique,
                                   isNullable,
                                   null));
      }
    }
  }

  // --------------------------------------------------------------------------------------------------------------------------
  private void AddChildRelationship(PropertyInfo p, bool isUnique, bool isNullable, TableDef? targetSet)
  {
    // NOTE: This seems like overkill.....
    var relationship = new TableRelationship(p.Name,
                                     targetSet.Name,
                                     nameof(IHasPrimary.ID),
                                     ERelationshipType.Child);

    var colDef = new ColumnDef(p.Name + "_" + nameof(IHasPrimary.ID),
                               typeof(int),
                               Schema.Flavor.TypeResolver.GetDataTypeName(typeof(int), false),
                               false,
                               isUnique,
                               isNullable,
                               relationship);


    this._ChildSets.Add(new DependentTable()
    {
      TargetSet = targetSet,
      PropertyPath = p.Name,
      Type = ERelationshipType.Child,
      ColDef = colDef
    });
  }

  // --------------------------------------------------------------------------------------------------------------------------
  private void AddParentRelationship(PropertyInfo p, bool isUnique, bool isNullable, TableDef? targetSet)
  {
    // NOTE: This seems like overkill.....
    var relationship = new TableRelationship(p.Name,
                                     targetSet.Name,
                                     nameof(IHasPrimary.ID),
                                     ERelationshipType.Parent);

    // This is the column that is going to be added to the parent table!
    var colDef = new ColumnDef(p.Name + "_" + nameof(IHasPrimary.ID),
                               typeof(int),
                               Schema.Flavor.TypeResolver.GetDataTypeName(typeof(int), false),
                               false,
                               isUnique,
                               isNullable,
                               relationship);

    // Since we have a list of entites, we are the child in the relationship.
    // We need to look to the parent to see what ids it has that match ours.
    // TODO: In order to really do many-many, we would have to add some data to our 'relationship'
    // attribute that could point to a specific property on the target data set....
    // Unless there is a list of entities that matches the name of this data set and has a named relationship....  We don't really have
    // a way to automatically resolve that at this time as not all properties have been populated.....
    // We would have to make another pass, maybe after setting up the 
    this._ParentSets.Add(new DependentTable()
    {
      TargetSet = targetSet,
      PropertyPath = p.Name,
      Type = ERelationshipType.Parent,
      ColDef = colDef,
    });
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public string GetCreateQuery()
  {
    var sb = new StringBuilder(0x400);
    sb.AppendLine($"CREATE TABLE IF NOT EXISTS {Name} (");

    var colDefs = new List<string>();
    var fkDefs = new List<string>();

    foreach (var col in Columns)
    {
      string useName = SchemaDefinition.FormatName(col.Name);

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

      if (col.Relationship != null)
      {

        string fk = $"FOREIGN KEY({useName}) REFERENCES {col.Relationship.RelatedTableName}({col.Relationship.RelatedTableColumn})";
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
      string colName = SchemaDefinition.FormatName(c.Name);

      if (c.IsPrimary)
      {
        pkName = colName;
        continue;
      }

      // NOTE: This makes no consideration for foreign keys, cols with defaults, etc.
      // We just throw them all in.
      colNames.Add(colName);
      colVals.Add("@" + c.Name);  // NOTE: We are using the same casing as the original datatype for the value parameters!
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
        if (c == colDef.Name)
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
  public string GetInsertQuery(string[]? useCols = null)
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
    if (RETURN_ID && namesAndVals.PrimaryKeyName != null)
    {
      sb.Append($" RETURNING {namesAndVals.PrimaryKeyName}");
    }

    string res = sb.ToString();
    return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public string GetUpdateQuery()
  {
    var useCols = (from x in this.Columns
                   where x.Relationship == null
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
    foreach (var rel in this._ParentSets)
    {
      // NOTE: This is where we would detect + add any many-many tables.
      var target = rel.TargetSet;
      // Find a parent that points to this table.  If there is one, then we have a situation
      // where we need to create a many-many table.  Don't care for now, so we will just blow up.
      foreach (var pSet in target.ParentSets)
      {
        if (pSet.TargetSet == this)
        {
          throw new NotSupportedException("auto-mapping of many-many tables is not supported at this time!");
        }
      }

      // If there is a single child relationship on the parent that points to this table,
      // then we don't need to emit anything.  We can assume that it is a bi-directional relationship.
      int matchCount = 0;
      foreach (var cSet in target.ChildSets)
      {
        if (cSet.TargetSet == this)
        {
          ++matchCount;
        }
      }
      if (matchCount == 1)
      {
        Log.Verbose("Found a single matching child relationship that matches this data set.  Bi-directional relationship assumed!");
        continue;
      }

      // If there are zero or more matches, then we want to emit the FK column.
      if (matchCount > 1)
      {
        Log.Verbose("There are multiple child relationships that match this set, no bi-directional relationship can be assumed.  Keys will be emitted for each!");
      }

      target.AddColumn(rel.ColDef);
    }

    foreach (var rel in this._ChildSets)
    {
      AddColumn(rel.ColDef);
      // this._Columns.Add(rel.ColDef);
    }

  }

  // --------------------------------------------------------------------------------------------------------------------------
  private void AddColumn(ColumnDef colDef)
  {
    // Only add the def if it doesn't already have 
    var match = (from x in _Columns where x.Name == colDef.Name select x).FirstOrDefault();
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
}

// ============================================================================================================================
public record ColumnDef(
  string Name,              // This is the same name as the property that this def comes from.
  Type RuntimeType,
  string DataType,
  bool IsPrimary,
  bool IsUnique,
  bool IsNullable,
  TableRelationship? Relationship)
{
  // --------------------------------------------------------------------------------------------------------------------------
  internal static bool AreSame(ColumnDef colDef, ColumnDef match)
  {
    bool res = (colDef.Name == match.Name &&
    colDef.IsPrimary == match.IsPrimary &&
    colDef.DataType == match.DataType &&
    colDef.IsUnique == match.IsUnique);

    return res;
  }
}

// ==========================================================================
public record TableRelationship
(
    string PropertyName,              // The name of the property on the type which this is defined.
    string RelatedTableName,
    string RelatedTableColumn,
    ERelationshipType RelationType
);

// ==========================================================================
public enum ERelationshipType
{
  Invalid = 0,
  Parent,
  Child
}

// ============================================================================================================================
/// <summary>
/// Describes a table that another is dependent upon.
/// This is your typical Foreign Key relationship in an RDBMS system.
/// </summary>
public class DependentTable
{
  public TableDef TargetSet { get; set; }
  public ERelationshipType Type { get; set; }

  /// <summary>
  /// The name of the property that contains the table in question.
  /// </summary>
  /// <value></value>
  /// <remarks>This only applies to child tables.</remarks>
  public string PropertyPath { get; set; } = string.Empty;

  public ColumnDef ColDef { get; set; } = null!;


  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// This tells us if we have a dependency on the given table, anywhere in the chain....
  /// </summary>
  internal bool HasTableDependency(TableDef t)
  {
    foreach (var dep in this.TargetSet.ParentSets)
    {
      if (dep.TargetSet.DataType == t.DataType)
      {
        return true;
      }
      if (dep.HasTableDependency(t))
      {
        return true;
      }
    }

    return false;
  }
}
