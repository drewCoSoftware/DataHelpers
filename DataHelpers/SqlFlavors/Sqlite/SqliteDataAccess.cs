using System.Diagnostics;
using System.Net.Http.Headers;
using Dapper;
using drewCo.Tools;
using Microsoft.Data.Sqlite;

namespace DataHelpers.Data;

// ========================================================================== 
public class SqliteDataAccess<TSchema> : IDataAccess
{
  // This is the ISO8601 format mentioned in:
  // https://www.sqlite.org/datatype3.html
  public const string SQLITE_DATETIME_FORMAT = "yyyy-MM-dd HH:mm:ss.fffffff";

  public string DataDirectory { get; private set; }
  public string DBFilePath { get; private set; }
  public string ConnectionString { get; private set; }

  private SchemaDefinition _Schema;
  public SchemaDefinition SchemaDef { get { return _Schema; } }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Insert the new instance into the database.
  /// The instance's ID property will be updated with the new ID in the database.
  /// </summary>
  public void InsertNew<T>(T instance)
    where T : IHasPrimary
  {
    if (instance.ID != 0)
    {
      throw new InvalidOperationException("This instance already has an ID!");
    }

    TableDef tableDef = SchemaDef.GetTableDef<T>(false)!;
    string query = tableDef.GetInsertQuery();
    int newID = RunSingleQuery<int>(query, instance);

    instance.ID = newID;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public SqliteDataAccess(string dataDir, string dbFileName)
  {
    DataDirectory = dataDir;
    DBFilePath = Path.Combine(DataDirectory, $"{dbFileName}.sqlite");
    ConnectionString = $"Data Source={DBFilePath};Mode=ReadWriteCreate";

    SqlMapper.RemoveTypeMap(typeof(DateTimeOffset));
    SqlMapper.AddTypeHandler<DateTimeOffset>(new DateTimeOffsetHandler());

    _Schema = new SchemaDefinition(new SqliteFlavor(), typeof(TSchema));

  }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// This makes sure that we have a database, and the schema is correct.
  /// </summary>
  public void SetupDatabase()
  {
    // Look at the current schema, and make sure that it is up to date....
    bool hasCorrectSchema = ValidateSchema();
    if (!hasCorrectSchema)
    {
      CreateDatabase();
    }
  }

  // --------------------------------------------------------------------------------------------------------------------------
  private void CreateDatabase()
  {
    string query = SchemaDef.GetCreateSQL();

    var conn = new SqliteConnection(ConnectionString);
    conn.Open();
    using (var tx = conn.BeginTransaction())
    {
      conn.Execute(query);
      tx.Commit();
    }
    conn.Close();
  }

  // --------------------------------------------------------------------------------------------------------------------------
  private bool ValidateSchema()
  {
    // Make sure that the file exists!
    var parts = ConnectionString.Split(";");
    foreach (var p in parts)
    {
      if (p.StartsWith("Data Source"))
      {
        string filePath = p.Split("=")[1].Trim();
        if (!File.Exists(filePath))
        {
          Debug.WriteLine($"The database file at: {filePath} does not exist!");
          return false;
        }
      }
    }

    var props = ReflectionTools.GetProperties<TSchema>();
    foreach (var p in props)
    {
      if (!HasTable(p.Name)) { return false; }
    }

    return true;
    // NOTE: This is simple.  In the future we could come up with a more robust verison of this.
    // bool res = HasTable(nameof(TimeManSchema.Sessions));
    // return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  private bool HasTable(string tableName)
  {
    // Helpful:
    // https://www.sqlite.org/schematab.html

    // NOTE: Later we can find a way to validate schema versions or whatever....
    var conn = new SqliteConnection(ConnectionString);
    conn.Open();
    string query = $"SELECT * from sqlite_schema where type = 'table' AND tbl_name=@tableName";

    var qr = conn.Query(query, new { tableName = tableName });
    bool res = qr.Count() > 0;
    conn.Close();

    return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Runs a database transaction, automatically rolling it back if there is an exception.
  /// </summary>
  protected void Transaction(Action<SqliteConnection> txWork)
  {
    using (var conn = new SqliteConnection(ConnectionString))
    {
      conn.Open();

      using (var tx = conn.BeginTransaction())
      {
        try
        {
          txWork(conn);
          tx.Commit();
        }
        catch (Exception ex)
        {
          // TODO: A better logging mechanism!
          Console.WriteLine($"An exception was encountered when trying to execute the transaction!");
          Console.WriteLine(ex.Message);
          Console.WriteLine("Transaction will be rolled back!");

          tx.Rollback();
        }
      }
    }
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public IEnumerable<T> RunQuery<T>(string query, object? qParams)
  {
    // NOTE: This connection object could be abstracted more so that we could handle
    // connection pooling, etc. as neeed.
    using (var conn = new SqliteConnection(ConnectionString))
    {
      conn.Open();
      var res = RunQuery<T>(conn, query, qParams);
      return res;
    }

  }

  // --------------------------------------------------------------------------------------------------------------------------
  protected IEnumerable<T> RunQuery<T>(SqliteConnection conn, string query, object? parameters)
  {
    var res = conn.Query<T>(query, parameters);
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

  // --------------------------------------------------------------------------------------------------------------------------
  public int RunExecute(string query, object? qParams)
  {
    using (var conn = new SqliteConnection(ConnectionString))
    {
      conn.Open();
      int res = RunExecute(conn, query, qParams);
      return res;
    }
  }

  // --------------------------------------------------------------------------------------------------------------------------
  protected int RunExecute(SqliteConnection conn, string query, object? qParams)
  {
    int res = conn.Execute(query, qParams);
    return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public TableAccess<TSchema> Table(string name)
  {
    var td = SchemaDef.GetTableDef(name, false)!;

    var res = new TableAccess<TSchema>(td, this);
    return res;
  }
}


// ==============================================================================================================================
public class TableAccess<TSchema>
{
  private TableDef Def = default!;
  private SqliteDataAccess<TSchema> DAL = default!;

  // --------------------------------------------------------------------------------------------------------------------------
  public TableAccess(TableDef def_, SqliteDataAccess<TSchema> dal_)
  {
    Def = def_;
    DAL = dal_;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public T Get<T>(int id) {
    string sql = Def.GetSelectByIDQuery();
    T res = DAL.RunSingleQuery<T>(sql, new { ID = id });
    return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public int Add(object data)
  {
    var dType = data.GetType();
    if (Def.DataType != dType)
    {
      throw new InvalidOperationException($"Input data is of type: {dType} but should be: {Def.DataType}!");
    }

    string sql = Def.GetInsertQuery();
    int res = DAL.RunSingleQuery<int>(sql, data);
    return res;
  }
}

// ==============================================================================================================================
interface ITable<T>
{
  public void Add<T>(T data);
}