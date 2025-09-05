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
    public SqliteDataAccess(string dataDir, string dbFileName)
    {
        DataDirectory = NormalizePathSeparators(dataDir);

        if (!dbFileName.EndsWith(".sqlite"))
        {
            dbFileName += ".sqlite";
        }

        DBFilePath = Path.Combine(DataDirectory, $"{dbFileName}");
        ConnectionString = $"Data Source={DBFilePath};Mode=ReadWriteCreate";

        SqlMapper.RemoveTypeMap(typeof(DateTimeOffset));
        SqlMapper.AddTypeHandler<DateTimeOffset>(new DateTimeOffsetHandler());

        _Schema = new SchemaDefinition(new SqliteFlavor(), typeof(TSchema));

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
    /// <summary>
    /// This makes sure that we have a database, and the schema is correct.
    /// </summary>
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
        using (var conn = GetConnection())
        {
            conn.Open();
            var res = RunQuery<T>(conn, query, qParams);
            return res;
        }

    }

    // --------------------------------------------------------------------------------------------------------------------------
    private SqliteConnection GetConnection()
    {
        // NOTE: This is where we might be able to use open connections, deal with transactions, etc.
        var res = new SqliteConnection(ConnectionString);
        return res;
    }

    // --------------------------------------------------------------------------------------------------------------------------
    protected IEnumerable<T> RunQuery<T>(SqliteConnection conn, string query, object? parameters)
    {
        var res = conn.Query<T>(query, parameters);

        // TODO: Open the connection here?

        return res;
    }

    // --------------------------------------------------------------------------------------------------------------------------
    /// <summary>
    /// Run a query where a single, or no result is expected.
    /// </summary>
    /// <remarks>
    /// If the query returns more than one result, and exception will be thrown.
    /// </remarks>
    public T? RunSingleQuery<T>(string query, object? parameters = null)
    {
        IEnumerable<T> qr = RunQuery<T>(query, parameters);
        T? res = qr.SingleOrDefault();
        return res;
    }

    // --------------------------------------------------------------------------------------------------------------------------
    public int RunExecute(string query, object? qParams = null)
    {
        using (var conn = new SqliteConnection(ConnectionString))
        {
            conn.Open();
            int res = RunExecute(conn, query, qParams);
            return res;
        }
    }

    // --------------------------------------------------------------------------------------------------------------------------
    protected int RunExecute(SqliteConnection conn, string query, object? qParams = null)
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
    public T? Get<T>(int id)
    {
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

        // Auto-assing the primary id.  This is kind of hacky, and I wish that this function was
        // actually generic or had better constraints.
        var pd = (data as IHasPrimary);
        if (pd != null) { pd.ID = res; }

        return res;
    }

    // --------------------------------------------------------------------------------------------------------------------------
    // TODO: Combine / align 'GetBy' functions.
    public T[] GetBy<T>(object example, IList<string> props)
    {
        string sql = Def.GetSelectByExampleQuery(example, props);

        var res = DAL.RunQuery<T>(sql, example);
        return res.ToArray();
    }

    // --------------------------------------------------------------------------------------------------------------------------
    // TODO: Add another GetByExample call where we already have the columns named?
    public T[] GetBy<T>(object example, string? orderBy = null, int pageNumber = 1, int pageSize = 20)
    {
        string sql = Def.GetSelectByExampleQuery(example);
        if (orderBy != null)
        {
            sql += $" ORDER BY {orderBy}";
        }

        int limit = pageSize;
        int offset = (pageNumber - 1) * pageSize;

        sql += $" LIMIT({limit}) OFFSET({offset})";

        var res = DAL.RunQuery<T>(sql, example);
        return res.ToArray();
    }

    // --------------------------------------------------------------------------------------------------------------------------
    /// <summary>
    /// Get the total number of items in the data set.
    /// </summary>
    public int GetItemCount()
    {
        string sql = $"SELECT COUNT(*) FROM {Def.Name}";
        int res = DAL.RunSingleQuery<int>(sql, null);
        return res;
    }

}

// ==============================================================================================================================
interface ITable<T>
{
    public void Add<T>(T data);
}