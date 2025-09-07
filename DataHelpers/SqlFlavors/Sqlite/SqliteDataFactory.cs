using Dapper;
using drewCo.Tools;
using Microsoft.Data.Sqlite;

namespace DataHelpers.Data;

// ==============================================================================================================================
public class SqliteDataFactory<TSchema> : DataFactory<TSchema, SqliteFlavor>
{
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

  private IDataAccess? InUse = null!;

  // --------------------------------------------------------------------------------------------------------------------------
  public override IDataAccess Data()
  {
    // I am trying to check for mulitple open transactions.....
    //if (InUse != null) { 
    //  InUse.
    //}
    var res = new SqliteDataAccess<TSchema>(ConnectionString, Schema, DataDirectory);
    return res;
  }


  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// This makes sure that we have a database, and the schema is correct.
  /// </summary>
  // TODO: This should be part of the 'IDataFactory' class/interface.
  public override void SetupDatabase()
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
          // LOG.WARNING
          Console.WriteLine($"The database file at: {filePath} does not exist!");
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
