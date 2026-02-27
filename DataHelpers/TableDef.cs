using drewCo.Tools;
using System.Text;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Linq.Expressions;
using drewCo.Tools.Logging;
using System.Reflection;
using System.Data;

namespace DataHelpers.Data;

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
  private ColumnDef? GetColumnByProperty(PropertyInfo p)
  {
    // Naive: related tables are not condsidered....
    // var res = (from x in this.Columns where x.PropertyName == p.Name select x).SingleOrDefault();

    string matchName = p.Name;  
    if (ReflectionTools.HasInterface<ISingleRelation>(p.PropertyType))
    {
      matchName = p.Name + "_ID";
    }
    // NOTE: This is where a column map would come in handy.  less digging around in loops!
    var res = (from x in this.Columns where x.PropertyName == matchName select x).SingleOrDefault();

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

      // We want to warn when single/multi-relations don't have the proper attribute.
      bool isRelationType = IsRelationType(p.PropertyType);
      if (isRelationType && relAttr == null)
      {
        Log.Warning($"The property: {p.Name} is a relation type, but doesn't have a {nameof(RelationAttribute)}!  It will not be included in the output set!");
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
  private bool IsRelationType(Type t)
  {
    //if (t.Name.StartsWith("Single"))
    //{
    //  int x = 10;
    //}
    if (t.Name.StartsWith(typeof(SingleRelation<>).Name) || t.Name.StartsWith(typeof(ManyRelation<>).Name))
    {
      return true;
    }
    return false;
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
      if (col.RelatedDataSet != null && Schema.Flavor.UsesInlineFKDeclaration)
      {
        def += $" REFERENCES {col.RelatedDataSet.TargetSet.Name} ({col.RelatedDataSet.TargetIDColumn.PropertyName})";
      }

      if (col.IsPrimary)
      {
        def += " PRIMARY KEY" + Schema.Flavor.GetIdentitySyntax(col);
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

      if (col.RelatedDataSet != null && !Schema.Flavor.UsesInlineFKDeclaration)
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


  // ==============================================================================================================================
  // REFACTOR: Relocate this if it ends up being useful.
  public class QueryBuilder
  {
    public string QueryType { get; private set; } = null!;

    public TableDef TableDef { get; private set; } = null!;

    class ColumnAndVal
    {
      public string ColumnName { get; set; }
      public string ParameterName { get; set; }
      public object? ColumnVal { get; set; }
      public DbType DbType { get; set; }
    }

    /// <summary>
    /// All columns and values that are associated with this query.
    /// NOTE: This may not be used in all cases.  In some queries, only the names are used, etc.
    /// </summary>
    private List<ColumnAndVal> MappedCols = new List<ColumnAndVal>();

    // --------------------------------------------------------------------------------------------------------------------------
    // TODO: Use an enum for the query type?
    public QueryBuilder(TableDef tableDef_, string queryType_)
    {
      QueryType = queryType_;
      TableDef = tableDef_;
    }

    // --------------------------------------------------------------------------------------------------------------------------
    public void AddColumn(string colName, string paramName, Type paramType, object? colVal = null)
    {
      this.MappedCols.Add(new ColumnAndVal
      {
        ColumnName = colName,
        ParameterName = paramName,
        DbType = TableDef.Schema.Flavor.ToDbType(paramType),
        ColumnVal = colVal
      });
    }

    // --------------------------------------------------------------------------------------------------------------------------
    public override string ToString()
    {
      var sb = new StringBuilder(0x800);
      string useType = QueryType.ToUpper();


      switch (useType)
      {
        case "INSERT":
          return BuildInsertQuery();
        default:
          throw new ArgumentOutOfRangeException($"Unknown query type: {useType}");
      }
    }

    // --------------------------------------------------------------------------------------------------------------------------
    private string BuildInsertQuery()
    {
      var sb = new StringBuilder(0x800);
      sb.Append($"INSERT INTO {TableDef.Name}");

      // NOTE: We should do a single loop to collect the column + parameter names.
      sb.Append($" ({string.Join(",", from x in this.MappedCols select x.ColumnName)})");
      sb.Append($" VALUES ({string.Join(",", from x in this.MappedCols select x.ParameterName)})");

      string res = sb.ToString();
      return res;
    }

    // --------------------------------------------------------------------------------------------------------------------------
    internal QueryParams GetQueryParams()
    {
      var res = new QueryParams();
      foreach (var item in this.MappedCols)
      {
        res.Add(item.ParameterName, new QueryParamValue(item.ColumnVal, item.DbType));
      }

      return res;
    }

    // --------------------------------------------------------------------------------------------------------------------------
    public QueryAndParams ToQueryAndParams()
    {
      var res = new QueryAndParams()
      {
        Query = this.ToString(),
        Params = this.GetQueryParams()
      };
      return res;
    }

  }


  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Create an insert query from the example instance.  This function is capable of omitting optional data members.
  /// </summary>
  public QueryAndParams GetInsertQueryFrom<T>(T instance, bool returnId = true)
  {
    var qb = new QueryBuilder(this, "INSERT");

    // To support optional (NULLABLE) params, we need to have direct knowledge of the instance -> column mappings.
    var props = DataType.GetProperties();
    foreach (var p in props)
    {
      // NOTE: This is where a property map comes into play:
      // NOTE: Single / multi relations certainly aren't included at this time...
      var match = GetColumnByProperty(p);


      if (match == null || match.PropertyName == nameof(IHasPrimary.ID)) { continue; }

      // Primary key values are not included!
      // if () { continue; }

      object? iVal = p.GetValue(instance);
      if (iVal == null)
      {
        if (!match.IsNullable)
        {
          throw new InvalidOperationException($"Column: {match.DataStoreName} requires a value, but property: {p.Name} is null!");
        }

        // We won't add anything here...
        continue;
      }

      // Add the column, param + value.
      qb.AddColumn(match.DataStoreName, $"@{p.Name}", p.PropertyType, iVal);
    }

    var res = qb.ToQueryAndParams();
    return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Generate an insert query that can be used to add records to this table.
  /// </summary>
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

  //public PropMap PropMap { get; private set; } = null!;
  //// --------------------------------------------------------------------------------------------------------------------------
  //internal void CreatePropertyMap()
  //{
  //  PropMap = new PropMap();

  //  foreach (var colDef in this.Columns)
  //  {
  //    // For the most part, only scalars get added to the property map!
  //    //if (colDef.PropInfo != null && ReflectionTools.IsSimpleType(colDef.RuntimeType))
  //    //{
  //    //  PropMap.Add(colDef.DataStoreName, colDef);
  //    //}
  //    // colDef.RuntimeType
  //  }
  //}


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

// ==============================================================================================================================
public class QueryAndParams
{
  public string Query { get; set; } = default!;
  public QueryParams Params { get; set; } = default!;
}
