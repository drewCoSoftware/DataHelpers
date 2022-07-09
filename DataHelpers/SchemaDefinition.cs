using drewCo.Tools;
using System.Text;
using System.Collections;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Reflection;
using System.Linq.Expressions;

namespace DataHelpers.Data;

// ============================================================================================================================
public class SchemaDefinition
{
  private object ResolveLock = new object();
  private Dictionary<string, TableDef> _TableDefs = new Dictionary<string, TableDef>(StringComparer.OrdinalIgnoreCase);
  private Dictionary<Type, TableDef> TypesToTableDef = new Dictionary<Type, TableDef>();
  public ReadOnlyCollection<TableDef> TableDefs { get { return new ReadOnlyCollection<TableDef>(_TableDefs.Values.ToList()); } }


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
        string right = GetExpressionSyntax(exp.Right);

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
    // We will add a table for each of the properties defined in 'schemaType'
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

    foreach (var def in _TableDefs.Values)
    {
      // Now we can populate all of the members.
      def.PopulateMembers();
    }

    ValidateSchema();

  }

  // --------------------------------------------------------------------------------------------------------------------------
  private void ValidateSchema()
  {
    foreach (var t in _TableDefs.Values)
    {
      // Circular reference test.
      foreach (var dep in t.ParentTables)
      {
        if (dep.HasTableDependency(t))
        {
          string msg = $"A circular reference from table: {t.Name} to: {dep.Def.Name} was detected!";
          throw new InvalidOperationException(msg);
        }
      }

      // Primary test.
      foreach (var pTable in t.ParentTables)
      {
        // The parent table MUST have a primary key!
        bool hasPrimary = ReflectionTools.HasInterface<IHasPrimary>(pTable.Def.DataType);
        if (!hasPrimary)
        {
          string msg = $"The data type: {pTable.Def.DataType} is a parent of {t.DataType}, but does not implement interface: {nameof(IHasPrimary)}";
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


  // --------------------------------------------------------------------------------------------------------------------------
  [Obsolete("Do not use this at this time!")]
  internal TableDef ResolveTableDef(string tableName, Type propertyType)
  {
    lock (ResolveLock)
    {
      if (_TableDefs.TryGetValue(tableName, out TableDef def))
      {
        if (def.DataType != propertyType)
        {
          throw new InvalidOperationException($"There is already a table named '{tableName}' with the data type '{def.DataType}'");
        }
        return def;
      }
      else
      {
        Type useType = propertyType;
        bool isList = ReflectionTools.HasInterface<IList>(useType);
        if (isList)
        {
          useType = useType.GetGenericArguments()[0];
        }
        var res = new TableDef(useType, tableName, this);
        _TableDefs.Add(tableName, res);
        res.PopulateMembers();

        return res;
      }
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
    res.Sort((l, r) => l.ParentTables.Count.CompareTo(r.ParentTables.Count));

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

  public ReadOnlyCollection<DependentTable> ParentTables { get { return new ReadOnlyCollection<DependentTable>(_ParentTables); } }
  private List<DependentTable> _ParentTables = new List<DependentTable>();

  public ReadOnlyCollection<DependentTable> ChildTables { get { return new ReadOnlyCollection<DependentTable>(_ChildTables); } }
  private List<DependentTable> _ChildTables = new List<DependentTable>();

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
      bool isNullable = ReflectionTools.HasAttribute<IsNullableAttribute>(p);

      // var parentAttr = ReflectionTools.GetAttribute<ParentRelationship>(p);
      // if (parentAttr != null)
      // {
      // }

      var childAttr = ReflectionTools.GetAttribute<ChildRelationship>(p);
      if (childAttr != null)
      {
        Type useType = p.PropertyType;
        bool isList = ReflectionTools.HasInterface<IList>(useType);
        if (isList)
        {
          useType = useType.GetGenericArguments()[0];
        }

        // Get the related table...
        var childDef = Schema.GetTableDef(useType);
        if (childDef == null)
        {
          throw new InvalidOperationException($"Could not resolve a table def for type {useType}!  Please check the schema!");
        }
        this._ChildTables.Add(new DependentTable()
        {
          Type = ERelationshipType.Child,
          Def = childDef,
          PropertyName = p.Name
        });

        // This is where we decide if we want a reference to a single item, or a list of them.
        string colName = $"{this.Name}_ID";
        string fkTableName = this.Name;
        var fkTableDef = this;

        var fkType = ReflectionTools.IsNullable(p.PropertyType) ? typeof(int?) : typeof(int);

        // NOTE: This is some incomplete work for many-many relationships, which we don't
        // actually support at this time.  Keep this block around for a while....
        //if (isList)
        //{
        //  // Resolve the mapping table....
        //  // what to do about the actual data type....  If we don't have a type defined,
        //  // should we just generate one?
        //  //object mappingTable = new { ParentID = (int)0, ChildID = (int)0 };
        //  //Type mappingTableType = mappingTable.GetType();

        //  fkTableName = $"{Name}_to_{relatedDef.Name}";
        //  Type mappingTableType = ResolveMappingTableType(DataType, useType);

        //  fkTableDef = Schema.ResolveTableDef(fkTableName, mappingTableType);
        //}
        ////else
        ////{
        ////  int x = 10;
        ////}
        ///

        childDef._ParentTables.Add(new DependentTable()
        {
          Def = fkTableDef,
          Type = ERelationshipType.Parent
        });


        childDef._Columns.Add(new ColumnDef(colName,
                                   Schema.Flavor.TypeResolver.GetDataTypeName(fkType),
                                   false,
                                   isUnique,
                                   isNullable,
                                   fkTableName,
                                   nameof(IHasPrimary.ID)));

      }
      else
      {
        // This is a normal column.
        // NOTE: Non-related lists can't be represented.... should we make it so that lists are always included?
        _Columns.Add(new ColumnDef(p.Name,
                                   Schema.Flavor.TypeResolver.GetDataTypeName(p.PropertyType),
                                   p.Name == nameof(IHasPrimary.ID),
                                   isUnique,
                                   isNullable,
                                   null,
                                   null));
      }
    }
  }

  // --------------------------------------------------------------------------------------------------------------------------
  private Type ResolveMappingTableType(Type parentType, Type childType)
  {
    // HACK: We won't always want a new instance of this....
    TypeGenerator gen = new TypeGenerator();
    Type res = gen.ResolveMappingTableType(parentType, childType);
    return res;
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
        def += " PRIMARY KEY";
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

      if (col.RelatedTableName != null)
      {
        string fk = $"FOREIGN KEY({useName}) REFERENCES {col.RelatedTableName}({col.RelatedTableColumn})";
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
  public string GetInsertQuery()
  {
    var colNames = new List<string>();
    var colVals = new List<string>();
    string? pkName = null;

    foreach (var c in this.Columns)
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

    StringBuilder sb = new StringBuilder(0x400);
    sb.Append($"INSERT INTO {this.Name} (");
    sb.Append(string.Join(",", colNames));

    sb.Append(") VALUES (");
    sb.Append(string.Join(",", colVals));

    sb.Append(")");

    // OPTIONS:
    const bool RETURN_ID = true;
    if (RETURN_ID && pkName != null)
    {
      sb.Append($" RETURNING {pkName}");
    }

    string res = sb.ToString();
    return res;
  }
}

// ============================================================================================================================
public record ColumnDef(string Name, string DataType, bool IsPrimary, bool IsUnique, bool IsNullable, string? RelatedTableName, string? RelatedTableColumn);

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
  public TableDef Def { get; set; }
  public ERelationshipType Type { get; set; }

  /// <summary>
  /// The name of the property that contains the table in question.
  /// </summary>
  /// <value></value>
  /// <remarks>This only applies to child tables.</remarks>
  public string PropertyName { get; set; } = string.Empty;

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// This tells us if we have a dependency on the given table, anywhere in the chain....
  /// </summary>
  internal bool HasTableDependency(TableDef t)
  {
    foreach (var dep in this.Def.ParentTables)
    {
      if (dep.Def.DataType == t.DataType)
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
