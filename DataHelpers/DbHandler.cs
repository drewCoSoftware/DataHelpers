using DataHelpers;
using DataHelpers.Data;
using drewCo.Tools;
using drewCo.Tools.Logging;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using BindCallback = System.Action<object>;

// ==============================================================================================================================
public class DHandler : IDisposable
{
  private SchemaDefinition SchemaDef = null!;

  private DbProviderFactory _DBProvider = null!;
  private string ConnectionString = null!;
  private DbConnection? Connection = null;
  private DbTransaction? Transaction = null;

  /// <summary>
  /// Cache of property maps for types.
  /// </summary>
  private static Dictionary<Type, PropMap> _Generated = new Dictionary<Type, PropMap>();
  private static object _PropMapLock = new object();


  private static Dictionary<Type, List<BindCallback>> _BindCallbacks = new Dictionary<Type, List<BindCallback>>();

  // --------------------------------------------------------------------------------------------------------------------------
  public DHandler(DbProviderFactory dbProvider_, string connectionString_, SchemaDefinition schemaDef_)
  {
    _DBProvider = dbProvider_;
    ConnectionString = connectionString_;
    SchemaDef = schemaDef_;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public void Dispose()
  {
    Transaction?.Commit();
    Transaction?.Dispose();
    Connection?.Dispose();
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public DbTransaction BeginTransaction()
  {
    var res = Connection.BeginTransaction();
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
    if (this.Connection == null)
    {
      Connection = _DBProvider.CreateConnection();
      if (Connection == null) { throw new InvalidOperationException("Could not create connection!"); }
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
  public int Execute(string sql, QueryParams qParams)
  {
    var conn = ResolveConnection();
    using (DbCommand cmd = conn.CreateCommand())
    {
      cmd.CommandText = sql;
      AddParameters(cmd, qParams);
      return cmd.ExecuteNonQuery();
    }
  }

  // --------------------------------------------------------------------------------------------------------------------------
  private static void AddParameters(DbCommand cmd, QueryParams? qParams)
  {
    if (qParams == null)
    {
      return;
    }

    foreach (KeyValuePair<string, object?> kvp in qParams)
    {
      // Accept keys with or without '@'
      string name = kvp.Key.StartsWith("@", StringComparison.Ordinal) ? kvp.Key : "@" + kvp.Key;

      DbParameter p = cmd.CreateParameter();
      p.ParameterName = name;
      p.Value = kvp.Value ?? DBNull.Value;
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


    while (reader.Read())
    {
      T item = Activator.CreateInstance<T>();

      var td = SchemaDef.GetTableDef<T>();
      foreach (KeyValuePair<string, int> kvp in ordinals)
      {
        var col = td.GetColumnByDataStoreName(kvp.Key);
        if (col == null)
        {
          continue;
        }

        var prop = col.PropInfo;
        if (prop == null)
        {
          if (col.RelationDef == null)
          {
            Log.Verbose($"The column named: {kvp.Key} on type: {td.Name} does not have a PropertyInfo or Relation!");
            continue;
          }

          MapRelationData(reader, item, kvp, col);

          continue;
        }

        // Normal property, nullable or otherwise.
        object? useVal = ResolveValue(reader, kvp.Value, prop.PropertyType);
        prop.SetValue(item, useVal);
      }

      // This is where we can do callbacks....
      if (_BindCallbacks.TryGetValue(typeof(T), out var callbacks))
      {
        foreach (var c in callbacks)
        {
          c.Invoke((T)item);
        }
      }

      res.Add(item);
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
  private void MapRelationData<T>(IDataReader reader, T item, KeyValuePair<string, int> kvp, ColumnDef col)
  {
    // We have a relation, so this is where we can create / populate that id....
    // We can resolve the data type, but I also need to be able to point this to a property on the current type....
    var rel = col.RelationDef;
    if (rel.RelationType == ERelationType.Single)
    {
      if (rel.TargetProperty == null)
      {
        throw new ArgumentNullException("A target property should be set on this relation!");
      }
    }
    else if (rel.RelationType == ERelationType.Many)
    {
      // NOTE: We aren't doing any specific checks here, but in the future when we want to do more
      // specific mappings we will definitely have to care about this stuff.
      Log.Warning("There is no specific support or checks for ManyRelations at this point!");
      //if (rel.TargetProperty != null)
      //{
      //  // This is probably a many->many, but I am not really sure if we want to do anything about that...
      //  // Umm.... this is a maybe, not sure what the conditions are ATM...
      //  throw new Exception("There is apparently data for a many relation on this dataset?  Is that right?");
      //}
    }

    var targetSet = SchemaDef.GetTableDef(rel.DataSetName);
    if (targetSet == null) { throw new NullReferenceException($"There is no data set named: {rel.DataSetName} in the schema!"); }

    // TODO: If we want, we can determine what the generic data type is....
    //if (targetSet.DataType != rel.TargetProperty.PropertyType)
    //{
    //  throw new InvalidOperationException($"The target data set type: {targetSet.DataType} does not match target property type: {rel.TargetProperty.PropertyType}!");
    //}
    // Use the ID type on 'IHasPrimary' interface.
    object? idVal = ResolveValue(reader, kvp.Value, typeof(int));
    if (idVal != null)
    {
      // Create the instance of the relation type + assign the ID.
      // TODO: At a later date we can decide if there is data in the result set (from a JOIN) where we would
      // populate the rest of the instance data.
      var instance = Activator.CreateInstance(rel.TargetProperty.PropertyType) as ISingleRelation; //    Activator.CreateInstance(targetSet.DataType) as IHasPrimary;
      instance.ID = (int)idVal;
      rel.TargetProperty.SetValue(item, instance);
    }
  }

  // --------------------------------------------------------------------------------------------------------------------------
  private object? ResolveValue(IDataReader reader, int value, Type dataType)
  {
    object? raw = reader.IsDBNull(value) ? null : reader.GetValue(value);
    if (raw == null) { return null; }

    Type targetType = Nullable.GetUnderlyingType(dataType) ?? dataType;
    object? converted = ConvertValue(raw, targetType);
    return converted;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  private PropMap GetColumnMap<T>()
  {
    // NOTE: I'm thinking that the property maps can even come from 'SchemaDef'!
    var td = SchemaDef.GetTableDef<T>();
    return td.PropMap;

    //lock (_PropMapLock)
    //{
    //  if (_Generated.TryGetValue(typeof(T), out var res))
    //  {
    //    return res;
    //  }

    //  PropertyInfo[] props = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public);
    //  Dictionary<string, PropertyInfo> propMap = new Dictionary<string, PropertyInfo>();
    //  foreach (PropertyInfo pi in props)
    //  {
    //    if (pi.CanWrite)
    //    {
    //      propMap[pi.Name] = pi;
    //    }
    //  }

    //  _Generated.Add(typeof(T), propMap);
    //  return propMap;
    //}

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

    if (targetType == typeof(DateTimeOffset) && value is DateTime dt)
    {
      return new DateTimeOffset(dt);
    }

    // Common change-type path (handles numeric conversions, strings, bools, DateTime, etc.)
    return Convert.ChangeType(value, targetType);
  }
}
