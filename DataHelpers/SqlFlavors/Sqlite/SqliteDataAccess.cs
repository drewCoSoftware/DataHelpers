using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Net.Http.Headers;
using Dapper;
using Microsoft.Data.Sqlite;

namespace DataHelpers.Data;


// ========================================================================== 
// NOTE: Always put this class in a 'using' block.
public class SqliteDataAccess<TSchema> : IDataAccess<TSchema>
{
  // This is the ISO8601 format mentioned in:
  // https://www.sqlite.org/datatype3.html
  public const string SQLITE_DATETIME_FORMAT = "yyyy-MM-dd HH:mm:ss.fffffff";

  public string DataDirectory { get; private set; }
  public string ConnectionString { get; private set; }

  private SchemaDefinition _Schema;
  public SchemaDefinition SchemaDef { get { return _Schema; } }


  private SqliteConnection Connection = null!;
  private SqliteTransaction? Transaction = null!;

  // --------------------------------------------------------------------------------------------------------------------------
  public SqliteDataAccess(string connectionString, SchemaDefinition schema_, string dataDir_)
  {
    ConnectionString = connectionString;
    _Schema = schema_;
    DataDirectory = dataDir_;

    Connection = new SqliteConnection(ConnectionString);
    Connection.Open();
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public void Dispose()
  {
    Transaction?.Commit();
    Transaction?.Dispose();
    Connection.Dispose();
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public SqliteTransaction BeginTransaction()
  {
    Transaction = Connection.BeginTransaction();
    return Transaction;
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
  public IEnumerable<T> RunQuery<T>(string query, object? qParams)
  {
    Dictionary<string,object>? dParams = null;
    if (qParams != null && !(qParams is QueryParams)) {
      dParams = Helpers.CreateParams(qParams);
    }
    var res = RunQuery<T>(Connection, query, dParams);
    return res;
  }

  //// --------------------------------------------------------------------------------------------------------------------------
  //public T? RunSingleQuery<T>(string query, QueryParams? qParams)
  //{
  //  var items = RunQuery<T>(query, qParams);
  //  var res = items.SingleOrDefault();
  //  return res;
  //}

    // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Run a query where a single, or no result is expected.
  /// </summary>
  /// <remarks>
  /// If the query returns more than one result, and exception will be thrown.
  /// </remarks>
  public T? RunSingleQuery<T>(string query, object? qParams = null)
  {
    IEnumerable<T> qr = RunQuery<T>(query, qParams);
    T? res = qr.SingleOrDefault();
    return res;
  }


  // --------------------------------------------------------------------------------------------------------------------------
  protected IEnumerable<T> RunQuery<T>(SqliteConnection conn, string query, Dictionary<string, object>? dParams)
  {
    DynamicParameters? useParams = null;
    if (dParams != null)
    {
      useParams = new DynamicParameters();
      foreach (var item in dParams)
      {
        useParams.Add(item.Key, item.Value);
      }
    }

    var res = conn.Query<T>(query, useParams);

    return res;
  }



  // --------------------------------------------------------------------------------------------------------------------------
  public int RunExecute(string query, object? qParams = null)
  {
    int res = RunExecute(Connection, query, qParams);
    return res;
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

  // --------------------------------------------------------------------------------------------------------------------------
  public void Rollback()
  {
    if (Transaction == null)
    {
      throw new InvalidOperationException("There is no transaction to roll back!");
    }
    Transaction.Rollback();
    Transaction.Dispose();
    Transaction = null;
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