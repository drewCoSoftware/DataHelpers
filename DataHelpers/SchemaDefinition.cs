using drewCo.Tools;
using System.Text;
using System.Collections;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace officepark.io.Data;

// ============================================================================================================================
public class SchemaDefinition
{
  private object ResolveLock = new object();
  private Dictionary<string, TableDef> _TableDefs = new Dictionary<string, TableDef>(StringComparer.OrdinalIgnoreCase);
  public ReadOnlyCollection<TableDef> TableDefs { get { return new ReadOnlyCollection<TableDef>(_TableDefs.Values.ToList()); } }


  // --------------------------------------------------------------------------------------------------------------------------
  public TableDef? GetTableDef(string tableName)
  {
    if (_TableDefs.TryGetValue(tableName, out TableDef? tableDef))
    {
      return tableDef;
    }
    else
    {
      return null;
    }
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

      // NOTE: Other attributes could be analyzed to change table names, etc.
      ResolveTableDef(prop.Name, useType);
    }
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public SchemaDefinition AddTable<T>()
  {
    string name = typeof(T).Name;
    return AddTable<T>(name);
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public SchemaDefinition AddTable<T>(string tableName)
  {
    ResolveTableDef(tableName, typeof(T));
    return this;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  internal bool HasTableDef(string tableName, Type propertyType)
  {
    if (_TableDefs.TryGetValue(tableName, out TableDef def))
    {
      return def.DataType == propertyType;
    }
    else
    {
      return false;
    }
  }


  // --------------------------------------------------------------------------------------------------------------------------
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
      sb.AppendLine(ComputeCreateTableQuery(d));
    }

    return sb.ToString();



    // throw new NotImplementedException();
  }

  // --------------------------------------------------------------------------------------------------------------------------
  private string ComputeCreateTableQuery(TableDef tableDef)
  {
    var sb = new StringBuilder(0x400);
    sb.AppendLine($"CREATE TABLE IF NOT EXISTS {tableDef.Name} (");

    var colDefs = new List<string>();
    var fkDefs = new List<string>();

    foreach (var col in tableDef.Columns)
    {
      string useName = FormatName(col.Name);

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
  /// <summary>
  /// NOTE: This should happen when we are building out our defs.
  /// NOTE: It should also be part of the current sql flavor too!
  /// </summary>
  public string FormatName(string name)
  {
    return name.ToLower();
  }

  // --------------------------------------------------------------------------------------------------------------------------
  private List<TableDef> SortDependencies(Dictionary<string, TableDef> tableDefs)
  {
    var res = new List<TableDef>(tableDefs.Values.ToList());

    // LOL, this probably won't work!
    // It would be nice if it was just a matter of counting.  This will suffice for now.
    res.Sort((l, r) => l.DependentTables.Count.CompareTo(r.DependentTables.Count));

    return res;
  }
}


// ============================================================================================================================
public class TableDef
{
  public Type DataType { get; private set; }
  public string Name { get; private set; }
  public SchemaDefinition Schema { get; private set; }

  public ReadOnlyCollection<DependentTable> DependentTables { get { return new ReadOnlyCollection<DependentTable>(_DependentTables); } }
  private List<DependentTable> _DependentTables = new List<DependentTable>();

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

      var dependent = ReflectionTools.GetAttribute<Relationship>(p);
      if (dependent != null)
      {
        Type useType = p.PropertyType;
        bool isList = ReflectionTools.HasInterface<IList>(useType);
        if (isList)
        {
          useType = useType.GetGenericArguments()[0];
        }

        // Get the related table...
        var relatedDef = Schema.ResolveTableDef(p.Name, useType);


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

        relatedDef._DependentTables.Add(new DependentTable()
        {
          Def = fkTableDef,
        });


        relatedDef._Columns.Add(new ColumnDef(colName,
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
  public string GetInsertQuery()
  {
    var colNames = new List<string>();
    var colVals = new List<string>();
    string? pkName = null;

    foreach (var c in this.Columns)
    {
      string colName = Schema.FormatName(c.Name);
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

// ============================================================================================================================
/// <summary>
/// Describes a table that another is dependent upon.
/// This is your typical Foreign Key relationship in an RDBMS system.
/// </summary>
public class DependentTable
{
  public TableDef Def { get; set; }
}
