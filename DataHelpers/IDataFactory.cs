using DataHelpers.Data;
using drewCo.Tools;
using System.Linq.Expressions;

namespace DataHelpers;

// ========================================================================== 
public interface IDataFactory<TSchema>
{
  /// <remarks>Make sure to DISPOSE the returned instance!  Put it in a using block!</remarks>
  IDataAccess<TSchema> GetDataAccess();
  // T Action<T>(Func<IDataAccess<TSchema>, T> action);
  void Action(Action<IDataAccess<TSchema>> action);

  TData Action<TData>(Func<IDataAccess<TSchema>, TData> action);

  void Transaction(Action<IDataAccess<TSchema>> action);
  void SetupDatabase();

  SchemaDefinition Schema { get; }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Delete the entity of the given type by its ID.
  /// </summary>
  int DeleteById<T>(int id)
    where T : IHasPrimary
  {
    if (id == 0) { throw new InvalidOperationException($"Invalid id!"); }
    var td = Schema.GetTableDef<T>();
    string query = $"DELETE FROM {td.Name} where id = @id";

    int res = Action(dal =>
    {
      return dal.RunExecute(query, new { id });
    });
    return res;

  }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Add a new entity to the database.
  /// </summary>
  int Add<T>(T entity)
    where T : IHasPrimary
  {
    if (entity.ID != 0) { throw new InvalidOperationException($"The entity already has an assigned ID and can't be added to the set!  Use 'AddOrUpdate' or 'Update' calls instead!"); }

    var td = Schema.GetTableDef<T>();
    var qParams = Schema.ComputeParametersFor(entity);
    string query = td.GetInsertQuery(qParams.Keys.ToArray());
    int res = Action(dal =>
    {
      int qr = dal.RunSingleQuery<int>(query, qParams);
      return qr;
    });

    entity.ID = res;

    // TODO: If there are manyassociations in the type, we want to update the mappings for them here!

    return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Get the entity of the given type by its ID
  /// </summary>
  T? GetById<T>(int id)
    where T : IHasPrimary
  {
    T? res = Action(dal =>
    {
      var td = Schema.GetTableDef<T>();
      string query = td.GetSelectQuery<T>() + " WHERE id = @id";
      return dal.RunSingleQuery<T>(query, new { id = id });
    });
    return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Get all instances of the given type that exists in the repository.
  /// DANGER: This can be very inefficient in certain cases.  Use cursors / pagination in those cases!
  /// </summary>
  T[] GetAll<T>()
  {
    return Action(dal =>
    {
      var td = Schema.GetTableDef<T>();
      string query = $"SELECT * FROM {td.Name}";

      var res = dal.RunQuery<T>(query);
      return res.ToArray();
    });
  }

  // --------------------------------------------------------------------------------------------------------------------------
  // TODO: Make a 'paged' version of this function (GetPaged)
  TEntity[] Get<TEntity>(Expression<Func<TEntity, bool>> predicate)
  {
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
  public T? GetBy<T>(params (string Name, object Value)[] properties)
  {
    return Action(dal =>
    {
      var td = dal.SchemaDef.GetTableDef<T>();
      string query = $"SELECT * FROM {td.Name}"; // WHERE name = @name";

      // TODO: SchemaDef should have its own QPB:
      var qb = new QueryParamsBuilder(dal.SchemaDef.Flavor);
      foreach (var item in properties)
      {
        qb.Add(item.Name, item.Value);
      }

      string wherePart = qb.GetWhereClause();
      query += (" " + wherePart);

      var qp = qb.Build();
      var res = dal.RunSingleQuery<T>(query, qp);
      return res;
    });
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
      if (key == nameof(IHasPrimary.ID)) { continue; }

      // NOTE: It would be a good thing if we could query the TableDef directly for this info.  Different SQL flavors
      // may have different name for their string columns, etc.
      var col = td.GetColumn(key);
      if (col == null) { throw new InvalidOperationException($"There is no column with key: {key}!"); }
      
      setParts.Add($"{key} = @{key}");

      var propType = col?.PropInfo?.PropertyType;

      if (propType == null)
      {
        // A bit of a HACK:, but we are going to assume that the FK is an integer!
        if (col.AssociationDef != null) {
          propType = typeof(int);
        }else { 
          throw new InvalidOperationException($"Could not resolve the data type for column: {col.DataStoreName}");
        }
      }
      qpBuilder.Add(key, value, propType);
    }
    string setClause = " SET " + string.Join(", ", setParts);
    string whereClause = $" WHERE {nameof(IHasPrimary.ID).ToLower()} = @id";

    string query = $"UPDATE {td.Name}" + setClause + whereClause;

    int updated = Action(dal =>
    {
      var qp = qpBuilder.Build();
      int res = dal.RunExecute(query, qp);
      return res;
    });

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
      if (item.Key == nameof(IHasPrimary.ID)) { continue; }

      var col = td.GetColumn(item.Key)!;
      setParts.Add($"{col.DataStoreName} = @{item.Key}");
    }

    string setClause = " SET " + string.Join(", ", setParts);
    string whereClause = $" WHERE {nameof(IHasPrimary.ID).ToLower()} = @ID";

    string query = $"UPDATE {td.Name}" + setClause + whereClause;


    Action(dal =>
    {
      int updated = dal.RunExecute(query, qParams);
      if (updated != 1)
      {
        throw new InvalidOperationException($"Could not update entity in data set: {td.Name}!");
      }
    });
  }

}

// ========================================================================== 
public abstract class IDataFactory<TSchema, TFlavor> : IDataFactory<TSchema>
  where TFlavor : ISqlFlavor, new()
{

  public SchemaDefinition Schema { get; private set; }

  // --------------------------------------------------------------------------------------------------------------------------
  public IDataFactory()
  {
    Schema = new SchemaDefinition(new TFlavor(), typeof(TSchema));
  }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Run an action against the IDataAccess instance.  Useful for reads or things that don't need
  /// to be in a transactions.
  /// </summary>
  [Obsolete("This will be removed.  Use other 'Action' override instead!")]
  public abstract IDataAccess<TSchema> GetDataAccess();

  /// <summary>
  /// Run an action against the IDataAccess instance inside of a transaction.  Useful
  /// for state-sensitive operations.
  /// </summary>
  public abstract void Transaction(Action<IDataAccess<TSchema>> action);

  public abstract void Action(Action<IDataAccess<TSchema>> action);
  public abstract TData Action<TData>(Func<IDataAccess<TSchema>, TData> action);

  public abstract void SetupDatabase();
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
      if (excludeProps?.Contains(p.Name) ?? false) { continue; }
      if (!p.CanWrite) { continue; }

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
