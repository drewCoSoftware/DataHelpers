using DataHelpers;
using DataHelpers.Data;
using drewCo.Curations;
using drewCo.Tools;
using drewCo.Tools.Logging;
using Microsoft.Data.Sqlite;
using Npgsql;
using System.ComponentModel.Design;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Linq.Expressions;
using BindCallback = System.Action<object>;

// ==============================================================================================================================
public class QueryParamValue
{
  // --------------------------------------------------------------------------------------------------------------------------
  public QueryParamValue(object? value_, DbType dataType_)
  {
    Value = value_;
    DataType = dataType_;

    if (DataType == DbType.DateTimeOffset && Value != null)
    {
      Value = ((DateTimeOffset)Value).ToUniversalTime();
    }
  }
  public object? Value { get; set; } = null;
  public DbType DataType { get; set; }
}

// ==============================================================================================================================
public class DBHandler<TSchema> : IDisposable
{
  public SchemaDefinition Schema { get; }

  private DbProviderFactory _DBProvider = null!;
  private string ConnectionString = null!;
  private DbConnection? Connection = null;
  private DbTransaction? Transaction = null;
  private bool IsDisposed;

  public bool HasTransaction => Transaction != null;

  /// <summary>
  /// Cache of property maps for types.
  /// </summary>
  private static Dictionary<Type, PropMap> _Generated = new Dictionary<Type, PropMap>();
  private static object _PropMapLock = new object();

  private static Dictionary<Type, List<BindCallback>> _BindCallbacks = new Dictionary<Type, List<BindCallback>>();



  // --------------------------------------------------------------------------------------------------------------------------
  public DBHandler(DbProviderFactory dbProvider_, string connectionString_, SchemaDefinition schemaDef_)
  {
    _DBProvider = dbProvider_;
    ConnectionString = connectionString_;
    Schema = schemaDef_;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public void Dispose()
  {
    if (IsDisposed)
    {
      return;
    }

    try
    {
      Commit();
    }
    finally
    {
      Transaction?.Dispose();
      Transaction = null;

      Connection?.Dispose();
      IsDisposed = true;
    }
  }

  public void Commit()
  {
    if (Transaction == null)
    {
      return;
    }

    Transaction.Commit();
    Transaction.Dispose();
    Transaction = null;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public DbTransaction BeginTransaction()
  {
    if (Transaction != null)
    {
      throw new InvalidOperationException("A transaction is already active!");
    }

    var conn = ResolveConnection();
    Transaction = conn.BeginTransaction();
    return Transaction;
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

  // --------------------------------------------------------------------------------------------------------------------------
  public static void RegisterCallback<T>(BindCallback cb)
  {

    if (!_BindCallbacks.TryGetValue(typeof(T), out List<BindCallback> callbacks))
    {
      callbacks = new List<BindCallback>();
      _BindCallbacks.Add(typeof(T), callbacks);
    }
    callbacks.Add(cb);
  }

  // --------------------------------------------------------------------------------------------------------------------------
  private DbConnection ResolveConnection()
  {
    ObjectDisposedException.ThrowIf(IsDisposed, this);

    if (this.Connection == null)
    {
      Connection = _DBProvider.CreateConnection();
      if (Connection == null)
      {
        throw new InvalidOperationException("Could not create connection!");
      }

      Connection.ConnectionString = this.ConnectionString;
      Connection.Open();
    }
    return Connection;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public IEnumerable<T> Query<T>(string query, QueryParams? qParams = null)
  {
    var conn = ResolveConnection();
    using (DbCommand cmd = conn.CreateCommand())
    {
      cmd.CommandText = query;
      AddParameters(cmd, qParams);

      using (IDataReader rdr = cmd.ExecuteReader())
      {
        return MapToList<T>(rdr);
      }
    }
  }


  // --------------------------------------------------------------------------------------------------------------------------
  public T? QuerySingle<T>(string query, QueryParams? qParams = null)
  {
    var list = Query<T>(query, qParams);
    return list.SingleOrDefault();
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public int Execute(string query, QueryParams qParams)
  {
    var conn = ResolveConnection();
    using (DbCommand cmd = conn.CreateCommand())
    {
      cmd.CommandText = query;
      AddParameters(cmd, qParams);
      return cmd.ExecuteNonQuery();
    }
  }

  // --------------------------------------------------------------------------------------------------------------------------
  // REFACTOR: Move this to the call site.
  public static int Execute(NpgsqlConnection conn, string query, QueryParams? qParams = null)
  {
    using (DbCommand cmd = conn.CreateCommand())
    {
      cmd.CommandText = query;
      AddParameters(cmd, qParams);
      return cmd.ExecuteNonQuery();
    }
  }

  // --------------------------------------------------------------------------------------------------------------------------
  // REFACTOR: Move this to the call site.
  public static int Execute(SqliteConnection conn, string query, QueryParams? qParams = null)
  {
    using (DbCommand cmd = conn.CreateCommand())
    {
      cmd.CommandText = query;
      AddParameters(cmd, qParams);
      return cmd.ExecuteNonQuery();
    }
  }

  // ==============================================================================================================================
  public class QueryResult
  {
    public List<Dictionary<string, object>> Rows { get; set; } = new List<Dictionary<string, object>>();
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public static QueryResult Query(NpgsqlConnection conn, string query, QueryParams? qParams = null)
  {
    using (DbCommand cmd = conn.CreateCommand())
    {
      var res = Query(cmd, query, qParams);
      return res;
    }
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public static QueryResult Query(SqliteConnection conn, string query, QueryParams? qParams = null)
  {
    using (DbCommand cmd = conn.CreateCommand())
    {
      var res = Query(cmd, query, qParams);
      return res;
    }
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public static QueryResult Query(DbCommand cmd, string query, QueryParams? qParams = null)
  {
    cmd.CommandText = query;
    AddParameters(cmd, qParams);

    var res = new QueryResult();

    using (IDataReader rdr = cmd.ExecuteReader())
    {
      var colNames = new List<string>();
      for (int i = 0; i < rdr.FieldCount; i++)
      {
        colNames.Add(rdr.GetName(i));
      }

      while (rdr.NextResult())
      {
        var row = new Dictionary<string, object>();
        for (int i = 0; i < rdr.FieldCount; i++)
        {
          row.Add(colNames[i], rdr.GetValue(i));
        }

        res.Rows.Add(row);
      }
    }

    return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public static void AddParameters(DbCommand cmd, QueryParams? qParams)
  {
    if (qParams == null)
    {
      return;
    }

    foreach (KeyValuePair<string, QueryParamValue> kvp in qParams)
    {
      // Accept keys with or without '@'
      string name = kvp.Key.StartsWith("@", StringComparison.Ordinal) ? kvp.Key : "@" + kvp.Key;

      DbParameter p = cmd.CreateParameter();
      p.ParameterName = name;
      p.Value = kvp.Value.Value ?? DBNull.Value;
      p.DbType = kvp.Value.DataType;
      cmd.Parameters.Add(p);
    }
  }

  // --------------------------------------------------------------------------------------------------------------------------
  // REFACTOR: This will should probably use a 'yield return' at some point, which will come in handy
  // for extra large queries...
  private IEnumerable<T> MapToList<T>(IDataReader reader)
  {
    var res = new List<T>();
    Dictionary<string, int> ordinals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    for (int i = 0; i < reader.FieldCount; i++)
    {
      ordinals[reader.GetName(i)] = i;
    }

    // Build writable property map.
    // NOTE: We should be able to get this from the schema / dataset def!
    // Having that def is what will make it so that we can map the special 'ID' properties back onto
    // the return type!
    // PropMap colMap = GetColumnMap<T>();

    // This is kind of a hack, but any time we are returning a primitive type, we can skip a lot of
    // extra processing / make different assumptions.
    if (ReflectionTools.IsSimpleType(typeof(T)))
    {
      if (reader.FieldCount > 1)
      {
        throw new NotSupportedException("This scenario is not supported!  We can only map primitive types when there is one column of results!");
      }

      return ReadScalarColumnData<T>(reader);

    }

    var td = Schema.TryGetTableDef<T>();

    // We are simply mapping out to whatever type.  Field names must match 1:1
    // NOTE: This do
    var allProps = ReflectionTools.GetProperties<T>();
    var lookup = new Dictionary<string, PropertyInfo>();
    foreach (var pi in allProps)
    {
      lookup.Add(pi.Name, pi);
    }

    while (reader.Read())
    {
      T instance = Activator.CreateInstance<T>();


      if (td != null)
      {
        foreach (KeyValuePair<string, int> kvp in ordinals)
        {
          // We will need to do some extra inspection to handle the joins.....
          // We will look for a '.' in the name to figure out what class instance it should go to.
          var col = td.GetColumnByDataStoreName(kvp.Key);
          if (col == null)
          {
            continue;
          }

          var prop = col.PropInfo;
          if (prop == null)
          {
            if (col.DependencyDef == null)
            {
              Log.Verbose($"The column named: {kvp.Key} on type: {td.Name} does not have a PropertyInfo or Association!");
              continue;
            }

            MapAssociationData(reader, instance, kvp, col);

            continue;
          }

          // Normal property, nullable or otherwise.
          object? useVal = ResolveValue(reader, kvp.Value, prop.PropertyType);
          prop.SetValue(instance, useVal);
        }

        // This is where we can do callbacks....
        if (_BindCallbacks.TryGetValue(typeof(T), out var callbacks))
        {
          foreach (var c in callbacks)
          {
            c.Invoke((T)instance);
          }
        }

      }
      else
      {

        // var lookup = DictionaryHelpers.PartitionIntoDictionary(allProps, x => x.Name);
        foreach (KeyValuePair<string, int> kvp in ordinals)
        {
          if (lookup.TryGetValue(kvp.Key, out var prop))
          {
            // Normal property, nullable or otherwise.
            object? useVal = ResolveValue(reader, kvp.Value, prop.PropertyType);
            prop.SetValue(instance, useVal);
          }
          else
          {
            Log.Debug($"There is no property named: {kvp.Key} on type: {typeof(T)}!");
            continue;
          }
        }
      }
      res.Add(instance);
    }

    return res;

  }



  // --------------------------------------------------------------------------------------------------------------------------
  private IEnumerable<T> ReadScalarColumnData<T>(IDataReader reader)
  {
    var res = new List<T>();
    while (reader.Read())
    {
      T next = (T)ResolveValue(reader, 0, typeof(T));
      res.Add(next);
    }
    return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  private void MapAssociationData<T>(IDataReader reader, T item, KeyValuePair<string, int> kvp, ColumnDef col)
  {
    // We have a association, so this is where we can create / populate that id....
    // We can resolve the data type, but I also need to be able to point this to a property on the current type....
    var rel = col.DependencyDef;
    if (rel.AssociationType == EAssociationType.Single)
    {
      if (rel.AssociationProperty == null)
      {
        throw new ArgumentNullException("A target property should be set on this association!");
      }
    }
    else if (rel.AssociationType == EAssociationType.Many)
    {
      // NOTE: We aren't doing any specific checks here, but in the future when we want to do more
      // specific mappings we will definitely have to care about this stuff.
      Log.Warning("There is no specific support or checks for Manyassociations at this point!");
      //if (rel.TargetProperty != null)
      //{
      //  // This is probably a many->many, but I am not really sure if we want to do anything about that...
      //  // Umm.... this is a maybe, not sure what the conditions are ATM...
      //  throw new Exception("There is apparently data for a many association on this dataset?  Is that right?");
      //}
    }

    var targetSet = rel.TargetSet; // Schema.GetTableDef(rel.DataSetName);
                                   // if (targetSet == null) { throw new NullReferenceException($"There is no data set named: {rel.DataSetName} in the schema!"); }

    // TODO: If we want, we can determine what the generic data type is....
    //if (targetSet.DataType != rel.TargetProperty.PropertyType)
    //{
    //  throw new InvalidOperationException($"The target data set type: {targetSet.DataType} does not match target property type: {rel.TargetProperty.PropertyType}!");
    //}
    // Use the ID type on 'IHasPrimary' interface.
    object? idVal = ResolveValue(reader, kvp.Value, typeof(int));
    if (idVal != null)
    {
      // Create the instance of the association type + assign the ID.
      // TODO: At a later date we can decide if there is data in the result set (from a JOIN) where we would
      // populate the rest of the instance data.
      var instance = Activator.CreateInstance(rel.AssociationProperty.PropertyType) as ISingleAssociation; //    Activator.CreateInstance(targetSet.DataType) as IHasPrimary;
      instance.ID = (int)idVal;
      rel.AssociationProperty.SetValue(item, instance);
    }
  }

  // --------------------------------------------------------------------------------------------------------------------------
  private object? ResolveValue(IDataReader reader, int value, Type dataType)
  {
    object? raw = reader.IsDBNull(value) ? null : reader.GetValue(value);
    if (raw == null)
    {
      return null;
    }

    object? converted = null!;
    Type targetType = Nullable.GetUnderlyingType(dataType) ?? dataType;
    if (ReflectionTools.HasInterface<ICompositeSerializer>(targetType))
    {
      // NOTE: We shouldn't have to create an instance of the class to deserialize it.
      // The interface should instead give us a 'strategy' that points to a singleton
      // that actually does the work.
      var cs = Activator.CreateInstance(targetType) as ICompositeSerializer;
      converted = cs.Deserialize(raw as string);
    }
    else
    {
      converted = ConvertValue(raw, targetType);
    }
    return converted;
  }

  private static object? ConvertValue(object value, Type targetType)
  {
    if (targetType.IsAssignableFrom(value.GetType()))
    {
      return value;
    }

    if (targetType.IsEnum)
    {
      if (value is string s)
      {
        return Enum.Parse(targetType, s, ignoreCase: true);
      }

      return Enum.ToObject(targetType, Convert.ChangeType(value, Enum.GetUnderlyingType(targetType))!);
    }

    if (targetType == typeof(Guid))
    {
      if (value is Guid g)
      {
        return g;
      }

      return Guid.Parse(Convert.ToString(value)!);
    }

    if (targetType == typeof(DateTimeOffset))
    {
      if (value is DateTime dt)
      {
        return new DateTimeOffset(dt);
      }
      else if (value is string)
      {
        if (DateTimeOffset.TryParse((string)value, out var dto))
        {
          return dto;
        }
        else
        {
          throw new InvalidOperationException($"Could not parse DateTimeOffset from string value: {(string)value}!");
        }

      }
      else
      {
        throw new InvalidOperationException("can't cast type to DateTimeOffset!");
      }
    }

    // Common change-type path (handles numeric conversions, strings, bools, DateTime, etc.)
    return Convert.ChangeType(value, targetType);
  }

  public void Action(Action<DBHandler<TSchema>> action)
  {
    action(this);
  }

  public TData Action<TData>(Func<DBHandler<TSchema>, TData> action)
  {
    return action(this);
  }

  public IEnumerable<T> RunQuery<T>(string query, object? qParams = null)
  {
    return Query<T>(query, Schema.ResolveQueryParams(qParams));
  }

  public T? RunSingleQuery<T>(string query, object? qParams = null)
  {
    return RunQuery<T>(query, qParams).SingleOrDefault();
  }

  public int RunExecute(string query, object? qParams = null)
  {
    return Execute(query, Schema.ResolveQueryParams(qParams));
  }

  public int GetCount<T>(string? criteria = null, object? qParams = null)
  {
    return RunSingleQuery<int>(Schema.GetCountQuery<T>(criteria), qParams);
  }

  public T[] GetBy<T>(object example, IList<string> props)
  {
    return RunQuery<T>(Schema.GetTableDef<T>().GetSelectByExampleQuery(example, props), example).ToArray();
  }

  public T[] GetBy<T>(object example, string? orderBy = null, int pageNumber = 1, int pageSize = 20)
  {
    if (pageNumber < 1)
    {
      throw new ArgumentOutOfRangeException(nameof(pageNumber));
    }

    if (pageSize < 1)
    {
      throw new ArgumentOutOfRangeException(nameof(pageSize));
    }

    string query = Schema.GetTableDef<T>().GetSelectByExampleQuery(example);

    if (orderBy != null)
    {
      query += $" ORDER BY {orderBy}";
    }

    query += $" LIMIT({pageSize}) OFFSET({(pageNumber - 1) * pageSize})";

    return RunQuery<T>(query, example).ToArray();
  }


  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Delete the entity of the given type by its ID.
  /// </summary>
  public int DeleteById<T>(int id)
    where T : IHasPrimary
  {
    if (id == 0)
    {
      throw new InvalidOperationException($"Invalid id!");
    }

    var td = Schema.GetTableDef<T>();
    string query = $"DELETE FROM {td.Name} where id = @id";

    return RunExecute(query, new { id });

  }

  // --------------------------------------------------------------------------------------------------------------------------
  public int Add<T>(T entity)
    where T : IHasPrimary
  {
    if (entity.ID != 0)
    {
      throw new InvalidOperationException($"The entity already has an assigned ID and can't be added to the set!  Use 'AddOrUpdate' or 'Update' calls instead!");
    }

    // Keep recursive association inserts on the same connection as the parent.
    return Add(typeof(T), entity, new HashSet<object>());
  }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Add a new entity to the database.
  /// The ID propery will be assigned upon successful creation.
  /// </summary>
  internal int Add(Type eType, IHasPrimary entity, HashSet<object> inQueue)
  {
    inQueue.Add(entity);

    var td = Schema.GetTableDef(eType);

    // Look for associations / dependent stuff....
    // For now, single associations only.

    AddUpdateAssociatedEntities(eType, entity, td, inQueue);

    var qParams = Schema.ResolveQueryParams(entity);
    string query = td.GetInsertQuery(qParams.Keys.ToArray());

    int res = RunSingleQuery<int>(query, qParams);

    entity.ID = res;

    // TODO: If there are manyassociations in the type, we want to update the mappings for them here!

    return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  internal void AddUpdateAssociatedEntities(Type eType, IHasPrimary entity, TableDef td, HashSet<object> inQueue)
  {
    foreach (var ad in td.Associations)
    {
      switch (ad.AssociationType)
      {
        case EAssociationType.Single:
          {
            // If we currently have an instance with an ID of zero, then we can think about adding it.
            var aVal = ad.AssociationProperty.GetValue(entity) as ISingleAssociation;
            if (aVal != null)
            {
              // We can recursively add this one?
              var data = aVal.GetData();

              if (inQueue.Contains(data.Value))
              {
                continue;
              }

              if (data.Value != null)
              {
                Type et = aVal.EntityType;
                if (data.Value.ID == 0)
                {
                  // This is a new, unsaved entity.
                  Add(et, data.Value, inQueue);
                  aVal.RefreshID();
                }
                else
                {
                  Log.Verbose("There is no support to auto-update an entity on Add() [Single]");
                }
              }
            }
          }
          break;

        case EAssociationType.Many:
          {
            Log.Verbose("Many association type auto-add is not supported!");
            continue;

            //var aVal = ad.AssociationProperty.GetValue(entity) as IManyAssociation;

            //if (aVal != null)
            //{
            //  Type et = aVal.EntityType;

            //  var data = aVal.GetItems();
            //  foreach (var item in data)
            //  {
            //    if (item.ID == 0)
            //    {
            //      // We need to somehow add a ref. back to the parent entity.... (entity)
            //      if (ad.Mutual != null)
            //      {
            //        var target = ad.Mutual.PropInfo.GetValue(item);
            //        var single = target as ISingleAssociation;
            //        if (single != null)
            //        {
            //          single.SetData(entity);
            //        }
            //        else
            //        {
            //          Log.Verbose($"Association target is not a {nameof(ISingleAssociation)} instance.  No other type is supported at this time!");
            //        }

            //      }
            //      if (inQueue.Contains(item)) { continue; }
            //      Add(et, item, inQueue);
            //    }
            //    else
            //    {
            //      Log.Verbose("There is no support to auto-update and entity on Add() [Many]");
            //    }
            //  }
            //}
          }
          break;

        default:
          throw new NotSupportedException();
      }

    }
  }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Get the entity of the given type by its ID
  /// </summary>
  public T? GetById<T>(int id)
    where T : IHasPrimary
  {
    var td = Schema.GetTableDef<T>();
    string query = td.GetSelectQuery<T>() + " WHERE id = @id";

    return RunSingleQuery<T>(query, new { id });
  }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Get all instances of the given type that exists in the repository.
  /// DANGER: This can be very inefficient in certain cases.  Use cursors / pagination in those cases!
  /// </summary>
  public T[] GetAll<T>()
  {
    var td = Schema.GetTableDef<T>();
    string query = $"SELECT * FROM {td.Name}";

    var res = RunQuery<T>(query);
    return res.ToArray();

  }

  // --------------------------------------------------------------------------------------------------------------------------
  // TODO: Make a 'paged' version of this function (GetPaged)
  public TEntity[] Get<TEntity>(Expression<Func<TEntity, bool>> predicate)
  {
    throw new NotImplementedException("this does not work in any way...");

    // throw new NotImplementedException("This is not working at this time!");
    int x = 10;

    Action(dal =>
    {
      // This is where we need to translate the predicate to the correct select, etc.
      // stuff that would make up the query.
      // string selectPart=
      if (predicate.NodeType == ExpressionType.Lambda)
      {
        if (predicate.Parameters.Count > 1)
        {
          throw new NotSupportedException("no support for multiple parameters!");
        }

        var param = predicate.Parameters[0];
        var body = predicate.Body;

        // We will want to split the body into all of sub-expressions so we can
        // figure out what tables, etc. are being used.

        // Decide what to do about the body....
        if (body.NodeType == ExpressionType.Equal)
        {
          var exp = body as BinaryExpression;

          // The part on the left is the property that we are accessing....
          // What we want to see if it is from a many association/mapped table.
          if (exp.Left.NodeType == ExpressionType.Parameter)
          {
            // We are doing a select on the table directly.

          }
          else
          {
            throw new NotImplementedException();
          }

          var l = exp.Left;
        }

      }
      else
      {
        throw new NotImplementedException("unsupported node type!");
      }

      // return null;
    });

    return null;

  }

  // --------------------------------------------------------------------------------------------------------------------------
  public IEnumerable<T> GetBy<T>(IList<(string Name, object Value)> props)
  {
    var td = Schema.GetTableDef<T>();
    string query = $"SELECT * FROM {td.Name}";

    // TODO: SchemaDef should have its own QPB:
    var qb = new QueryParamsBuilder(Schema.Flavor);
    foreach (var item in props)
    {
      qb.Add(item.Name, item.Value);
    }

    // TODO: We need support for "x IS NULL" because SQL is fucking stupid"
    string wherePart = qb.GetWhereClause();
    query += (" " + wherePart);

    var qp = qb.Build();
    var res = RunQuery<T>(query, qp);

    return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public IEnumerable<T> GetBy<T>(params (string Name, object Value)[] properties)
  {
    return GetBy<T>(properties.ToList());
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public T? GetBySingle<T>(params (string Name, object Value)[] properties)
  {
    var td = Schema.GetTableDef<T>();
    string query = $"SELECT * FROM {td.Name}"; // WHERE name = @name";

    // TODO: SchemaDef should have its own QPB:
    var qb = new QueryParamsBuilder(Schema.Flavor);
    foreach (var item in properties)
    {
      qb.Add(item.Name, item.Value);
    }

    string wherePart = qb.GetWhereClause();
    query += (" " + wherePart);

    var qp = qb.Build();
    var res = RunSingleQuery<T>(query, qp);

    return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  // REFACTOR:  This function is general enough that it should be part of the SQLFlavor implementations.
  // SHARE:  This belongs with DataHelpers.
  public void UpdateEntity<T>(int entityId, params (string key, object? value)[] propsAndValues)
    where T : IHasPrimary
  {
    // TODO: Make sure the schema actually has the type in questions....
    var td = Schema.GetTableDef<T>()!;

    var t = typeof(T);

    var qpBuilder = new QueryParamsBuilder(Schema.Flavor);
    qpBuilder.Add("id", entityId);

    var setParts = new List<string>();
    foreach (var (key, value) in propsAndValues)
    {
      // NOTE: We don't have any null support for this!

      // DO NOT CHANGE THE ID OR ATTEMPT TO!
      if (key == nameof(IHasPrimary.ID))
      {
        continue;
      }

      // NOTE: It would be a good thing if we could query the TableDef directly for this info.  Different SQL flavors
      // may have different name for their string columns, etc.
      var col = td.GetColumn(key);
      if (col == null)
      {
        throw new InvalidOperationException($"There is no column with key: {key}!");
      }

      setParts.Add($"{key} = @{key}");

      var propType = col?.PropInfo?.PropertyType;

      if (propType == null)
      {
        // A bit of a HACK:, but we are going to assume that the FK is an integer!
        if (col.DependencyDef != null)
        {
          propType = typeof(int);
        }
        else
        {
          throw new InvalidOperationException($"Could not resolve the data type for column: {col.DataStoreName}");
        }
      }
      qpBuilder.Add(key, value, propType);
    }

    string setClause = " SET " + string.Join(", ", setParts);
    string whereClause = $" WHERE {nameof(IHasPrimary.ID).ToLower()} = @id";

    string query = $"UPDATE {td.Name}" + setClause + whereClause;

    var qp = qpBuilder.Build();
    int updated = RunExecute(query, qp);

    if (updated != 1)
    {
      throw new InvalidOperationException($"Could not update entity in data set: {td.Name}!");
    }

  }

  // --------------------------------------------------------------------------------------------------------------------------
  // REFACTOR: This should be off in the DataHelpers lib, somewhere.
  public void UpdateEntity<T>(int entityId, EditData editData)
    where T : IHasPrimary
  {
    // TODO: Make sure the schema actually has the type in questions....
    var td = Schema.GetTableDef<T>()!;

    var t = typeof(T);

    var qParams = editData.ToQueryParams(Schema.Flavor);
    var setParts = new List<string>();

    foreach (var item in qParams)
    {
      if (item.Key == nameof(IHasPrimary.ID))
      {
        continue;
      }

      var col = td.GetColumn(item.Key)!;
      setParts.Add($"{col.DataStoreName} = @{item.Key}");
    }

    string setClause = " SET " + string.Join(", ", setParts);
    string whereClause = $" WHERE {nameof(IHasPrimary.ID).ToLower()} = @ID";

    string query = $"UPDATE {td.Name}" + setClause + whereClause;

    qParams["ID"] = new QueryParamValue(entityId, System.Data.DbType.Int32);

    int updated = RunExecute(query, qParams);
    if (updated != 1)
    {
      throw new InvalidOperationException($"Could not update entity in data set: {td.Name}!");
    }
  }

}
