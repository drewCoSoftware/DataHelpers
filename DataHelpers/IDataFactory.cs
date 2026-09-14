using DataHelpers.Data;
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

    // TODO: If there are manyrelations in the type, we want to update the mappings for them here!

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
      if (predicate.NodeType == ExpressionType.Lambda) { 
      if (predicate.Parameters.Count > 1) { 
        throw new NotSupportedException("no support for multiple parameters!"); 
      }



        var param = predicate.Parameters[0];
        var body = predicate.Body;

        // We will want to split the body into all of sub-expressions so we can
        // figure out what tables, etc. are being used.



        // Decide what to do about the body....
        if (body.NodeType == ExpressionType.Equal) { 
          var exp = body as BinaryExpression;

          // The part on the left is the property that we are accessing....
          // What we want to see if it is from a manyrelation/mapped table.
          if (exp.Left.NodeType == ExpressionType.Parameter) { 
            // We are doing a select on the table directly.

          }
          else
          {
            throw new NotImplementedException();
          }
          var l = exp.Left;

          int sfsdf = 10;
        }
        int x = 10;

      }
      else {
        throw new NotImplementedException("unsupported node type!");
      }

      // return null;
    });

    return null;

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
