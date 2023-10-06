using System.Reflection;
using DataHelpers.Data;
using Npgsql;
using Dapper;
using drewCo.Tools;
using System.Text;
using NpgsqlTypes;
using System.Diagnostics;

// ========================================================================== 
public class PostgresDataAccess : IDataAccess
{
  public SchemaDefinition SchemaDef => throw new NotImplementedException();

  public string ConnectionString { get; private set; }
  private string? DatabaseName { get; set; } = null;
  private bool IsDefaultDatabase = true;   // use the default 'postgres' database when one isn't specifically set in the connection string.

  // -----------------------------------------------------------------------------------------------
  public PostgresDataAccess(string connectionString_)
  {
    this.ConnectionString = connectionString_;

    string[] parts = ConnectionString.Split(";");
    foreach (var p in parts)
    {
      string[] kvpParts = p.Split("=");
      if (kvpParts[0].Equals("database", StringComparison.OrdinalIgnoreCase))
      {
        DatabaseName = kvpParts[1];
        IsDefaultDatabase = false;
      }
    }

    if (IsDefaultDatabase)
    {
      ConnectionString += "Database=postgres";
    }
  }

  // -----------------------------------------------------------------------------------------------
  public NpgsqlConnection CreateConnection()
  {
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(ConnectionString);
    NpgsqlDataSource dataSource = dataSourceBuilder.Build();
    NpgsqlConnection res = dataSource.CreateConnection();
    return res;
  }

  // -----------------------------------------------------------------------------------------------
  public int BulkInsert<T>(TableDef tableDef, IEnumerable<T> toInsert)
  {
    int count = toInsert.Count();
    if (count == 0)
    {
      // Do nothing....
      return 0;
    }
    Dictionary<string, ColumnDef> columns = new Dictionary<string, ColumnDef>();
    Dictionary<string, PropertyInfo> props = new Dictionary<string, PropertyInfo>();

    NamesAndValues namesAndValues = tableDef.GetNamesAndValues(false);
    List<string> valNamesBase = namesAndValues.ColValues;
    List<string> propNames = namesAndValues.ColNames;

    // We should get this off of the tabeDef....
    // The problem is that we can enumerating over the data....
    var lProps = ReflectionTools.GetProperties<T>();

    //    var lProps = ReflectionTools.GetProperties<T>();
    foreach (var col in tableDef.Columns)
    {
      // Some calls may be filtered out according to the def (identify PKs for example)
      //      if (col.IsPrimary) { continue; }
      if (!propNames.Contains(col.Name)) { continue; }

      var match = (from x in lProps
                   where x.Name == col.Name
                   select x).SingleOrDefault();
      if (match != null)
      {
        string colName = col.Name;
        columns.Add(colName, col);
        props.Add(colName, match);
      }
    }

    var sb = new StringBuilder(0x4000);
    string insertPart = tableDef.GetInsertPart();
    sb.Append(insertPart + Environment.NewLine);
    sb.Append(" VALUES " + Environment.NewLine);

    int index = 0;
    foreach (var item in toInsert)
    {
      string nameParts = string.Join(", ", (from x in valNamesBase
                                            select x + "_" + index));
      string valPart = "(" + nameParts + ")";
      sb.Append(valPart);

      ++index;
      if (index < count)
      {
        sb.Append("," + Environment.NewLine);
      }
    }

    int res = -1;
    using (var conn = CreateConnection()) //  new PostgresConnection(ConnectionString))
    {

      conn.Open();

      NpgsqlCommand cmd = conn.CreateCommand();
      cmd.CommandText = sb.ToString();

      // Now add all of the parameters.
      index = 0;
      foreach (var item in toInsert)
      {
        foreach (var c in propNames)
        {
          string pName = SchemaDefinition.FormatName(c);
          pName += "_" + index;

          var prop = props[c];
          object? val = prop.GetValue(item);

          var param = new NpgsqlParameter()
          {
            ParameterName = pName,
            Value = val ?? DBNull.Value
          };
          cmd.Parameters.Add(param);
        }

        ++index;
      }

      res = cmd.ExecuteNonQuery();
      conn.Close();
    }

    return res;

  }

  // TODO: This should be put on the column defs, probably.
  private NpgsqlDbType GetDbType(Type propertyType)
  {
    if (propertyType == typeof(string))
    {
      return NpgsqlDbType.Varchar;
    }
    else
    {
      Debug.WriteLine($"No translations for data type: {propertyType} to {nameof(NpgsqlDbType)}");
      return NpgsqlDbType.Unknown;
    }
  }

  // -----------------------------------------------------------------------------------------------
  public IEnumerable<T> RunQuery<T>(string query, object? qParams)
  {

    // var dataSourceBuilder = new NpgsqlDataSourceBuilder(ConnectionString);
    // var dataSource = dataSourceBuilder.Build();
    using (var conn = CreateConnection()) //  new PostgresConnection(ConnectionString))
    {
      conn.Open();
      var res = RunQuery<T>(conn, query, qParams);
      conn.Close();
      return res;
    }

  }

  // --------------------------------------------------------------------------------------------------------------------------
  protected IEnumerable<T> RunQuery<T>(NpgsqlConnection conn, string query, object? parameters)
  {

    // We will fix any datetimeoffset parametesr to have a UTC offset which is required
    // by postgresql.
    if (parameters != null)
    {
      var props = ReflectionTools.GetProperties(parameters.GetType());
      foreach (var p in props)
      {
        if (p.PropertyType == typeof(DateTimeOffset) || p.PropertyType == typeof(DateTimeOffset?))
        {
          if (!p.CanWrite)
          {
            Console.WriteLine($"Warning!  DatetimeOffset value for property {p.Name} is not writable!  Operation will fail if date offset is not zero!");
          }
          object? val = p.GetValue(parameters);
          if (val != null)
          {
            DateTimeOffset useVal = ((DateTimeOffset)val).ToUniversalTime();
            p.SetValue(parameters, useVal);
          }
        }
      }
    }


    var res = conn.Query<T>(query, parameters);
    return res;

  }


  // -----------------------------------------------------------------------------------------------
  public int RunExecute(string query, object? qParams)
  {
    using (var conn = CreateConnection())
    {
      conn.Open();
      int res = RunExecute(conn, query, qParams);

      conn.Close();
      return res;
    }
  }

  // --------------------------------------------------------------------------------------------------------------------------
  protected int RunExecute(NpgsqlConnection conn, string query, object? qParams)
  {
    int res = conn.Execute(query, qParams);
    return res;
  }


  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Run a query where a single, or no result is expected.
  /// </summary>
  /// <remarks>
  /// If the query returns more than one result, and exception will be thrown.
  /// </remarks>
  public T? RunSingleQuery<T>(string query, object? parameters)
  {
    IEnumerable<T> qr = RunQuery<T>(query, parameters);
    T? res = qr.SingleOrDefault();
    return res;
  }


  // public T? RunSingleQuery<T>(string query, object? parameters)
  // {
  //   throw new NotImplementedException();
  // }
}

// ========================================================================== 
public class PostgresDataAccess<TSchema> : PostgresDataAccess
{
  // This is the ISO8601 format mentioned in:
  // https://www.sqlite.org/datatype3.html
  public const string SQLITE_DATETIME_FORMAT = "yyyy-MM-dd HH:mm:ss.fffffff";


  private SchemaDefinition _Schema;
  public SchemaDefinition SchemaDef { get { return _Schema; } }


  // -----------------------------------------------------------------------------------------------
  public PostgresDataAccess(string connectionString_)
    : base(connectionString_)
  {
    //    ConnectionString = connectionString_;


    // // TODO: Add data type mapping as needed:
    // SqlMapper.RemoveTypeMap(typeof(DateTimeOffset));
    // SqlMapper.AddTypeHandler<DateTimeOffset>(new DateTimeOffsetHandler());

    _Schema = new SchemaDefinition(new PostgresFlavor(), typeof(TSchema));
  }


  // // -----------------------------------------------------------------------------------------------
  // public int RunExecute(string query, object? qParams)
  // {
  //   using (var conn = CreateConnection())
  //   {
  //     conn.Open();
  //     int res = RunExecute(conn, query, qParams);
  //     return res;
  //   }
  // }

  // // --------------------------------------------------------------------------------------------------------------------------
  // protected int RunExecute(NpgsqlConnection conn, string query, object? qParams)
  // {
  //   int res = conn.Execute(query, qParams);
  //   return res;
  // }


  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// This makes sure that we have a database, and the schema is correct.
  /// </summary>
  public void SetupDatabase()
  {
    // Look at the current schema, and make sure that it is up to date....
    bool hasCorrectSchema = ValidateSchemaExists();
    if (!hasCorrectSchema)
    {
      CreateDatabase();
    }
  }

  // --------------------------------------------------------------------------------------------------------------------------
  private void CreateDatabase()
  {
    string query = SchemaDef.GetCreateSQL();

    using (var conn = CreateConnection())
    {
      conn.Open();
      using (var tx = conn.BeginTransaction())
      {
        conn.Execute(query);
        tx.Commit();
      }
      conn.Close();
    }
  }


  // --------------------------------------------------------------------------------------------------------------------------
  private bool ValidateSchemaExists()
  {
    return false;


    // NOTE: I am not really sure how ask postgres what databases it may or may not have.....
    // We can worry about this later as we don't need it right now.
    using (var conn = CreateConnection())
    {
    }

    return true;
    //   // Make sure that the file exists!
    //   var parts = ConnectionString.Split(";");
    //   foreach (var p in parts)
    //   {
    //     if (p.StartsWith("Data Source"))
    //     {
    //       string filePath = p.Split("=")[1].Trim();
    //       if (!File.Exists(filePath))
    //       {
    //         Debug.WriteLine($"The database file at: {filePath} does not exist!");
    //         return false;
    //       }
    //     }
    //   }

    //   var props = ReflectionTools.GetProperties<TSchema>();
    //   foreach (var p in props)
    //   {
    //     if (!HasTable(p.Name)) { return false; }
    //   }

    //   return true;
    //   // NOTE: This is simple.  In the future we could come up with a more robust verison of this.
    //   // bool res = HasTable(nameof(TimeManSchema.Sessions));
    //   // return res;
  }


}