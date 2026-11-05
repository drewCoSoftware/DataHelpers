using System.Linq;
using drewCo.Tools;
using drewCo.Tools.Logging;
using Microsoft.Data.Sqlite;

namespace DataHelpers.Data;

// ==============================================================================================================================
public class SqliteDataFactory<TSchema> : IDataFactory<TSchema, SqliteFlavor>
{
  public SchemaDefinition Schema { get; } = new SchemaDefinition(new SqliteFlavor(), typeof(TSchema));

  public string DataDirectory { get; private set; }
  public string DBFilePath { get; private set; }
  public string ConnectionString { get; private set; }

  // --------------------------------------------------------------------------------------------------------------------------
  public SqliteDataFactory(string dataDir, string dbFileName)
  {
    DataDirectory = NormalizePathSeparators(dataDir);

    if (!dbFileName.EndsWith(".sqlite"))
    {
      dbFileName += ".sqlite";
    }

    DBFilePath = Path.Combine(DataDirectory, $"{dbFileName}");
    ConnectionString = $"Data Source={DBFilePath};Mode=ReadWriteCreate";
  }

  // --------------------------------------------------------------------------------------------------------------------------
  private DBHandler? ActiveHandler;
  private bool HasTransaction;

  private TResult WithHandler<TResult>(Func<DBHandler, TResult> action)
  {
    if (ActiveHandler != null)
    {
      return action(ActiveHandler);
    }

    using (var handler = new DBHandler(SqliteFactory.Instance, ConnectionString, Schema))
    {
      return action(handler);
    }
  }

  public IEnumerable<T> RunQuery<T>(string query, object? qParams = null)
  {
    return WithHandler(handler => handler.Query<T>(query, Schema.ResolveQueryParams(qParams)).ToArray());
  }

  public T? RunSingleQuery<T>(string query, object? qParams = null)
  {
    return RunQuery<T>(query, qParams).SingleOrDefault();
  }

  public int RunExecute(string query, object? qParams = null)
  {
    return WithHandler(handler => handler.Execute(query, Schema.ResolveQueryParams(qParams)));
  }

  public void Rollback()
  {
    if (ActiveHandler == null)
    {
      throw new InvalidOperationException("There is no transaction to roll back!");
    }

    ActiveHandler.Rollback();
    HasTransaction = false;
  }

  public void Action(Action<IDataFactory<TSchema, SqliteFlavor>> action)
  {
    Action<int>(factory =>
    {
      action(factory);

      return 0;
    });
  }

  public TData Action<TData>(Func<IDataFactory<TSchema, SqliteFlavor>, TData> action)
  {
    if (ActiveHandler != null)
    {
      return action(this);
    }

    using (var handler = new DBHandler(SqliteFactory.Instance, ConnectionString, Schema))
    {
      var scope = (SqliteDataFactory<TSchema>)MemberwiseClone();
      scope.ActiveHandler = handler;

      return action(scope);
    }
  }

  public void Transaction(Action<IDataFactory<TSchema, SqliteFlavor>> action)
  {
    if (ActiveHandler != null)
    {
      throw new InvalidOperationException("Nested transactions are not supported!");
    }

    using (var handler = new DBHandler(SqliteFactory.Instance, ConnectionString, Schema))
    {
      var scope = (SqliteDataFactory<TSchema>)MemberwiseClone();
      scope.ActiveHandler = handler;

      handler.BeginTransaction();
      scope.HasTransaction = true;

      try
      {
        action(scope);
      }
      catch
      {
        if (scope.HasTransaction)
        {
          handler.Rollback();
        }

        throw;
      }
    }
  }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// This makes sure that we have a database, and the schema is correct.
  /// </summary>
  // TODO: This should be part of the 'IDataFactory' class/interface.
  public void SetupDatabase()
  {
    // Look at the current schema, and make sure that it is up to date....
    bool hasCorrectSchema = ValidateSchema();
    if (!hasCorrectSchema)
    {
      FileTools.CreateDirectory(DataDirectory);
      CreateDatabase();
    }
  }

  // --------------------------------------------------------------------------------------------------------------------------
  private void CreateDatabase()
  {
    string query = Schema.GetCreateSQL();
    var conn = new SqliteConnection(ConnectionString);
    conn.Open();
    using (var tx = conn.BeginTransaction())
    {
      DBHandler.Execute(conn, query);

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
          // LOG.WARNING
          Console.WriteLine($"The database file at: {filePath} does not exist!");
          return false;
        }
      }
    }

    var props = ReflectionTools.GetProperties<TSchema>();
    foreach (var p in props)
    {
      if (!HasTable(p.Name))
      {
        return false;
      }
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

    var qParams = new QueryParams();
    qParams.Add(nameof(tableName), new QueryParamValue(tableName, System.Data.DbType.String));
    var qr = DBHandler.Query(conn, query, qParams);

    // var qr = conn.Query(query, new { tableName = tableName });
    bool res = qr.Rows.Count > 0;
    conn.Close();

    return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  // TODO: SHARE: Put this in the tools lib!
  /// <summary>
  /// Normalize the path separators in 'dir' so they match the OS.
  /// </summary>
  [Obsolete("Use version from derwco.tools > 1.4.1")]
  public static string NormalizePathSeparators(string dir)
  {
    if (Path.DirectorySeparatorChar == '\\')
    {
      return dir.Replace('/', '\\');
    }
    else if (Path.DirectorySeparatorChar == '/')
    {
      return dir.Replace('\\', '/');
    }
    else
    {
      throw new ArgumentOutOfRangeException($"Unsupported directory separator character: '{Path.DirectorySeparatorChar}'!");
    }
  }

}
