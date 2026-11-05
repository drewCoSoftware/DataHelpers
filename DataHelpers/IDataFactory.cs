using DataHelpers.Data;
using drewCo.Tools;
using System.Linq.Expressions;

namespace DataHelpers;

public interface IDataFactory<TSchema, TFlavor>
  where TFlavor : ISqlFlavor, new()
{
  public SchemaDefinition Schema { get; }

  public void Action(Action<DBHandler<TSchema>> action);
  public TData Action<TData>(Func<DBHandler<TSchema>, TData> action);

  /// <summary>Operations on the callback handler share one transaction.</summary>
  public void Transaction(Action<DBHandler<TSchema>> action);
  public void SetupDatabase();

  public IEnumerable<T> RunQuery<T>(string query, object? qParams = null)
  {
    return Action(db => db.RunQuery<T>(query, qParams));
  }

  public T? RunSingleQuery<T>(string query, object? qParams = null)
  {
    return Action(db => db.RunSingleQuery<T>(query, qParams));
  }

  public int RunExecute(string query, object? qParams = null)
  {
    return Action(db => db.RunExecute(query, qParams));
  }

  public int GetCount<T>(string? criteria = null, object? qParams = null)
  {
    return Action(db => db.GetCount<T>(criteria, qParams));
  }

  public T[] GetBy<T>(object example, IList<string> props)
  {
    return Action(db => db.GetBy<T>(example, props));
  }

  public T[] GetBy<T>(object example, string? orderBy = null, int pageNumber = 1, int pageSize = 20)
  {
    return Action(db => db.GetBy<T>(example, orderBy, pageNumber, pageSize));
  }

  public int DeleteById<T>(int id)
    where T : IHasPrimary
  {
    return Action(db => db.DeleteById<T>(id));
  }

  public int Add<T>(T entity)
    where T : IHasPrimary
  {
    return Action(db => db.Add(entity));
  }

  public T? GetById<T>(int id)
    where T : IHasPrimary
  {
    return Action(db => db.GetById<T>(id));
  }

  public T[] GetAll<T>()
  {
    return Action(db => db.GetAll<T>());
  }

  public TEntity[] Get<TEntity>(Expression<Func<TEntity, bool>> predicate)
  {
    return Action(db => db.Get(predicate));
  }

  public IEnumerable<T> GetBy<T>(IList<(string Name, object Value)> props)
  {
    return Action(db => db.GetBy<T>(props));
  }

  public IEnumerable<T> GetBy<T>(params (string Name, object Value)[] properties)
  {
    return Action(db => db.GetBy<T>(properties));
  }

  public T? GetBySingle<T>(params (string Name, object Value)[] properties)
  {
    return Action(db => db.GetBySingle<T>(properties));
  }

  public void UpdateEntity<T>(int entityId, params (string key, object? value)[] propsAndValues)
    where T : IHasPrimary
  {
    Action(db => db.UpdateEntity<T>(entityId, propsAndValues));
  }

  public void UpdateEntity<T>(int entityId, EditData editData)
    where T : IHasPrimary
  {
    Action(db => db.UpdateEntity<T>(entityId, editData));
  }
}

// ==============================================================================================================================
// REFACTOR:  This type is very similar to that of QueryParams....
// It's like an unflavored version, so I wonder why we have it.....
// I need to inspect the two to figure out what is going on....
// --> @@AAR: 9/17/2026 : I think that the best way is to replace this type with QueryParamsBuilder,
// and update that type so that it's 'build' function is what takes in the flavor argument.  That keeps it all nice and portable.
public class EditData
{
  // --------------------------------------------------------------------------------------------------------------------------
  public EditData() { }

  // --------------------------------------------------------------------------------------------------------------------------
  public static EditData CreateFrom<T>(T instance, IList<string>? excludeProps = null)
  {
    var res = new EditData()
    {
      TypeName = typeof(T).Name,
    };
    var props = ReflectionTools.GetProperties(typeof(T));
    foreach (var p in props)
    {
      if (excludeProps?.Contains(p.Name) ?? false)
      {
        continue;
      }

      if (!p.CanWrite)
      {
        continue;
      }

      object? val = p.GetValue(instance);
      res.AddProperty(p.Name, p.PropertyType, val);
    }

    return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public static EditData Create<T>()
  {
    var res = new EditData()
    {
      TypeName = typeof(T).Name,
    };
    return res;
  }

  /// <summary>
  /// The data, or entity type that is being edited...
  /// </summary>
  public string TypeName { get; set; }

  /// <summary>
  /// All of the properties that are being changed....
  /// How do we manage compound / nested types...?
  /// I'm thinking just full property paths.... Works everywhere else, and we have
  /// the technology so......
  /// </summary>
  //public List<KeyValuePair<string, string>> Properties { get; set; }
  // public Dictionary<string, string?> Properties = new Dictionary<string, string?>();

  public Dictionary<string, (Type, object?)> Properties = new Dictionary<string, (Type, object?)>();

  // --------------------------------------------------------------------------------------------------------------------------
  public T? Get<T>(string propName)
  {
    var match = Properties[propName];
    var res = (T?)match.Item2;
    return res;
  }

  //// --------------------------------------------------------------------------------------------------------------------------
  //internal void AddProperty(string propName, object? propVal)
  //{
  //  AddProperty(propName, propVal?.ToString());
  //}

  // --------------------------------------------------------------------------------------------------------------------------
  public void AddProperty(string propName, Type propType, object? val)
  {
    // if (propVal == null) { throw new ArgumentNullException($"{nameof(propVal)} should not be null!"); }
    Properties.Add(propName, (propType, val));
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public void ValidateProperty<T>(string propName, Func<T?, bool>? predicate = null)
  {
    if (!Properties.ContainsKey(propName))
    {
      throw new InvalidOperationException($"The property: {propName} does not exist in the edit data!");
    }

    if (predicate != null)
    {
      T? val = Get<T>(propName);
      bool isValid = predicate(val);
      if (!isValid)
      {
        throw new InvalidOperationException($"The value for property: {propName} is not valid!");
      }
    }
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public QueryParams ToQueryParams(ISqlFlavor flavor)
  {
    var qpBuilder = new QueryParamsBuilder(flavor);

    foreach (var kv in Properties)
    {
      qpBuilder.Add(kv.Key, kv.Value.Item2, kv.Value.Item1);
    }

    var res = qpBuilder.Build();
    return res;
  }

}

// ==============================================================================================================================
public interface IHasType
{
  Type DataType { get; }
}

// ==============================================================================================================================
public interface IValueOrNull : IHasType
{
  public object? Value { get; }
}

// ==============================================================================================================================
public class ValueOrNull<T> : IValueOrNull
{
  // --------------------------------------------------------------------------------------------------------------------------
  public ValueOrNull(object? value_)
  {
    Value = value_;
    if (Value != null)
    {
      if (Value.GetType() != typeof(T))
      {
        throw new InvalidOperationException("Value is not the correct type!");
      }
    }
  }

  public object? Value { get; }
  public Type DataType
  {
    get
    {
      Type t = typeof(T);
      return t;
    }
  }
}

//// ==============================================================================================================================
///// <summary>
///// A way to represent 'null' in GetBy calls....
///// </summary>
//public class NullValue<T> : IHasType
//{
//  public Type DataType { get { return typeof(T); } }
//}
