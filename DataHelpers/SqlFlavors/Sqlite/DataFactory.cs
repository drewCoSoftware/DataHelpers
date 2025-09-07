using Dapper;

namespace DataHelpers.Data;

// ========================================================================== 
public interface IDataFactory
{
  IDataAccess Transaction();
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
  public abstract IDataAccess Transaction();
  public abstract void SetupDatabase();
}
