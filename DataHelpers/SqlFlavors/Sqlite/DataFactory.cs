using Dapper;

namespace DataHelpers.Data;

// ========================================================================== 
public interface IDataFactory
{
  IDataAccess Action();
  void Transaction(Action<IDataAccess> action);
  void SetupDatabase();

  SchemaDefinition Schema { get; }
}

// ========================================================================== 
public abstract class DataFactory<TSchema, TFlavor> : IDataFactory
  where TFlavor : ISqlFlavor, new()
{

  public SchemaDefinition Schema { get; private set; }

  // --------------------------------------------------------------------------------------------------------------------------
  public DataFactory()
  {
    Schema = new SchemaDefinition(new TFlavor(), typeof(TSchema));

    SqlMapper.RemoveTypeMap(typeof(DateTimeOffset));
    SqlMapper.AddTypeHandler<DateTimeOffset>(new DateTimeOffsetHandler());
  }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Run an action against the IDataAccess instance.  Useful for reads or things that don't need
  /// to be in a transactions.
  /// </summary>
  public abstract IDataAccess Action();

  /// <summary>
  /// Run an action against the IDataAccess instance inside of a transaction.  Useful
  /// for state-sensitive operations.
  /// </summary>
  public abstract void Transaction(Action<IDataAccess> action);

  public abstract void SetupDatabase();
}
