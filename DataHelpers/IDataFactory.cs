using Dapper;
using DataHelpers.Data;

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

    SqlMapper.RemoveTypeMap(typeof(DateTimeOffset));
    SqlMapper.AddTypeHandler(new DateTimeOffsetHandler());
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
