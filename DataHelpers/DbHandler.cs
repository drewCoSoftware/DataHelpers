using DataHelpers.Data;
using System.Data;
using System.Data.Common;
using System.Reflection;

// ==============================================================================================================================
public class DHandler : IDisposable
{
  private SchemaDefinition SchemaDef = null!;

  private DbProviderFactory _DBProvider = null!;
  private string ConnectionString = null!;
  private DbConnection? Connection = null;
  private DbTransaction? Transaction = null;

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
  public List<T> Query<T>(string query, QueryParams? qParams = null)
  where T : new()
  {
    // factory.
    var conn = ResolveConnection();
    //if (conn == null)
    //{
    //  throw new InvalidOperationException("Failed to create connection.");
    //}

    //conn.ConnectionString = connectionString;
    //conn.Open();

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
  where T : new()
  {
    var list = Query<T>(query, qParams);
    return list.SingleOrDefault();
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public static int Execute(DbProviderFactory factory, string connectionString, string sql, QueryParams qParams)
  {
    using (DbConnection conn = factory.CreateConnection())
    {
      if (conn == null)
      {
        throw new InvalidOperationException("Failed to create connection.");
      }

      conn.ConnectionString = connectionString;
      conn.Open();

      using (DbCommand cmd = conn.CreateCommand())
      {
        cmd.CommandText = sql;
        AddParameters(cmd, qParams);
        return cmd.ExecuteNonQuery();
      }
    }
  }

  public static object? Scalar(DbProviderFactory factory, string connectionString, string sql, QueryParams qParams)
  {
    using (DbConnection conn = factory.CreateConnection())
    {
      if (conn == null)
      {
        throw new InvalidOperationException("Failed to create connection.");
      }

      conn.ConnectionString = connectionString;
      conn.Open();

      using (DbCommand cmd = conn.CreateCommand())
      {
        cmd.CommandText = sql;
        AddParameters(cmd, qParams);
        object? val = cmd.ExecuteScalar();
        return val is DBNull ? null : val;
      }
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
  private static List<T> MapToList<T>(IDataReader reader)
    where T : new()
  {
    List<T> results = new List<T>();
    Dictionary<string, int> ordinals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    for (int i = 0; i < reader.FieldCount; i++)
    {
      ordinals[reader.GetName(i)] = i;
    }

    // Build writable property map.
    // NOTE: We should be able to get this from the schema / dataset def!
    // Having that def is what will make it so that we can map the special 'ID' properties back onto
    // the return type!
    PropertyInfo[] props = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public);
    Dictionary<string, PropertyInfo> propMap = new Dictionary<string, PropertyInfo>();
    foreach (PropertyInfo pi in props)
    {
      if (pi.CanWrite)
      {
        propMap[pi.Name] = pi;
      }
    }

    while (reader.Read())
    {
      T item = new T();

      foreach (KeyValuePair<string, int> kvp in ordinals)
      {
        if (!propMap.TryGetValue(kvp.Key, out PropertyInfo? prop))
        {
          continue;
        }

        object? raw = reader.IsDBNull(kvp.Value) ? null : reader.GetValue(kvp.Value);
        if (raw == null)
        {
          prop.SetValue(item, null);
          continue;
        }

        Type targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

        try
        {
          object? converted = ConvertValue(raw, targetType);
          prop.SetValue(item, converted);
        }
        catch
        {
          // Silent skip on conversion issues, or throw if preferred
          // throw; 
        }
      }

      results.Add(item);
    }

    return results;
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
