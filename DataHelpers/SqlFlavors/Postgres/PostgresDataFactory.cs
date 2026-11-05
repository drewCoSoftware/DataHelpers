using System.Linq;
﻿using DataHelpers.Data;
using drewCo.Tools;
using Npgsql;
using drewCo.Tools.Logging;

namespace DataHelpers.SqlFlavors.Postgres
{
  // ==============================================================================================================================
  public class PostgresDataFactory<TSchema> : IDataFactory<TSchema, PostgresFlavor>
  {
    public SchemaDefinition Schema { get; } = new SchemaDefinition(new PostgresFlavor(), typeof(TSchema));

    private string Host = default!;
    private string DBName = default!;
    private string Username = default!;
    private string Password = default!;
    private int Port = default!;

    private string ConnectionString = default!;

    // --------------------------------------------------------------------------------------------------------------------------
    public PostgresDataFactory(string connectionString_)
    {
      ConnectionString = connectionString_;
    }

    // --------------------------------------------------------------------------------------------------------------------------
    public PostgresDataFactory(string host_, string dbName_, string username_, string password_, int port_)
    {
      Host = host_;
      DBName = dbName_;
      Username = username_;
      Password = password_;
      Port = port_;

      ConnectionString = $"Server={Host};Port={Port};Database={DBName};User ID={Username};Password={Password};Include Error Detail=false";
    }

    // --------------------------------------------------------------------------------------------------------------------------
    public void Action(Action<DBHandler<TSchema>> action)
    {
      using (var handler = new DBHandler<TSchema>(NpgsqlFactory.Instance, ConnectionString, Schema))
      {
        action(handler);
      }
    }

    public TData Action<TData>(Func<DBHandler<TSchema>, TData> action)
    {
      using (var handler = new DBHandler<TSchema>(NpgsqlFactory.Instance, ConnectionString, Schema))
      {
        return action(handler);
      }
    }

    public void Transaction(Action<DBHandler<TSchema>> action)
    {
      using (var handler = new DBHandler<TSchema>(NpgsqlFactory.Instance, ConnectionString, Schema))
      {
        handler.BeginTransaction();

        try
        {
          action(handler);
          handler.Commit();
        }
        catch
        {
          if (handler.HasTransaction)
          {
            handler.Rollback();
          }

          throw;
        }
      }
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
      string query = Schema.GetCreateSQL();

      using (var conn = OpenConnection())
      {
        using (var tx = conn.BeginTransaction())
        {
          DBHandler<TSchema>.Execute(conn, query);
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

      using (var conn = OpenConnection())
      {
        string query = $"SELECT * from sqlite_schema where type = 'table' AND tbl_name=@tableName";

        // var qp = new QueryParamsBuilder(this.fla
        var qParams = new QueryParams();
        qParams.Add(nameof(tableName), new QueryParamValue(tableName, System.Data.DbType.String));
        var qr = DBHandler<TSchema>.Query(conn, query, qParams);
        // var qr = conn.Query(query, new { tableName = tableName });
        bool res = qr.Rows.Count > 0; // Count() > 0;
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
