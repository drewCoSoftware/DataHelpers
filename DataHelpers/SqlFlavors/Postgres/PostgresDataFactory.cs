using DataHelpers.Data;
using drewCo.Tools.Logging;
using drewCo.Tools;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using drewCo;
using Npgsql;
using Dapper;

namespace DataHelpers.SqlFlavors.Postgres
{
  // ==============================================================================================================================
  public class PostgresDataFactory<TSchema> : IDataFactory<TSchema, SqliteFlavor>
  {
    //    public string DataDirectory { get; private set; }
    //  public string DBFilePath { get; private set; }
    // public string ConnectionString { get; private set; }

    private string Host = default!;
    private string DBName = default!;
    private string Username = default!;
    private string Password = default!;
    private int Port = default!;

    private string ConnectionString = default!;


    // --------------------------------------------------------------------------------------------------------------------------
    public PostgresDataFactory(string host_, string dbName_, string username_, string password_, int port_)
    {
      Host = host_;
      DBName = dbName_;
      Username = username_;
      Password = password_;
      Port = port_;

      //string dbHost = Environment.GetEnvironmentVariable("AWR_DB_HOST")!;
      //string dbName = Environment.GetEnvironmentVariable("AWR_DATABASE")!;
      //string dbUser = Environment.GetEnvironmentVariable("AWR_USER")!;
      //string dbPass = Environment.GetEnvironmentVariable("AWR_DB_PASSWORD")!;

      ConnectionString = $"Server={Host};Port={Port};Database={DBName};User ID={Username};Password={Password};Include Error Detail=false";


      //PGAccess = new PostgresDataAccess(pgConnection);
      //SchemaDef = new SchemaDefinition(new PostgresFlavor(), typeof(AWRDBSchema));

      //  DataDirectory = NormalizePathSeparators(dataDir);

      //if (!dbFileName.EndsWith(".sqlite"))
      //{
      //  dbFileName += ".sqlite";
      //}

      //  DBFilePath = Path.Combine(DataDirectory, $"{dbFileName}");
      // ConnectionString = $"Data Source={DBFilePath};Mode=ReadWriteCreate";
    }

    // --------------------------------------------------------------------------------------------------------------------------
    public override void Action(Action<IDataAccess<TSchema>> action)
    {
      using (var dal = new PostgresDataAccess<TSchema>(ConnectionString))
      {
        action(dal);
      }
    }

    // --------------------------------------------------------------------------------------------------------------------------
    public override TData Action<TData>(Func<IDataAccess<TSchema>, TData> action)
    {
      using (var dal = new PostgresDataAccess<TSchema>(ConnectionString))
      {
        TData res = action(dal);
        return res;
      }
    }


    // --------------------------------------------------------------------------------------------------------------------------
    public override void Transaction(Action<IDataAccess<TSchema>> action)
    {   
      throw new NotImplementedException();

      //using (var dataAccess = new PostgresDataAccess<TSchema>(ConnectionString))
      //{
      //  var tx = dataAccess.BeginTransaction();
      //  try
      //  {
      //    action(dataAccess);
      //  }
      //  catch (Exception ex)
      //  {
      //    Log.Exception(ex);
      //    Log.Warning("The transaction will be rolled back!");

      //    tx.Rollback();
      //  }
      //}
    }

    // --------------------------------------------------------------------------------------------------------------------------
    [Obsolete("This will be removed in a future iteration!")]
    public override IDataAccess<TSchema> GetDataAccess()
    {
      var res = new PostgresDataAccess<TSchema>(ConnectionString);
      return res;
    }


    private NpgsqlDataSource _DataSource = null!;
    private NpgsqlDataSource DataSource
    {
      get
      {
        return _DataSource ?? (_DataSource = CreateDataSource());
      }
    }

    // --------------------------------------------------------------------------------------------------------------------------
    private NpgsqlDataSource CreateDataSource()
    {
      var dataSourceBuilder = new NpgsqlDataSourceBuilder(ConnectionString);
      NpgsqlDataSource dataSource = dataSourceBuilder.Build();
      return dataSource;
    }


    // -----------------------------------------------------------------------------------------------
    public NpgsqlConnection OpenConnection()
    {
      NpgsqlConnection res = DataSource.OpenConnection();
      return res;
    }

    // --------------------------------------------------------------------------------------------------------------------------
    /// <summary>
    /// This makes sure that we have a database, and the schema is correct.
    /// </summary>
    public override void SetupDatabase()
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
      string query = Schema.GetCreateSQL();

      using (var conn = OpenConnection())
      {
        using (var tx = conn.BeginTransaction())
        {
          // conn.Execute(query);
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
      using (var conn = OpenConnection())
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
      /// var conn = OpenConnection()  new SqliteConnection(ConnectionString);
      // conn.Open();

      using (var conn = OpenConnection())
      {
        string query = $"SELECT * from sqlite_schema where type = 'table' AND tbl_name=@tableName";

        var qr = conn.Query(query, new { tableName = tableName });
        bool res = qr.Count() > 0;
        conn.Close();

        return res;
      }
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

}
