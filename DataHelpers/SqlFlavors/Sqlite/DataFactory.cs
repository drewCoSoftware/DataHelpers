using Dapper;

namespace DataHelpers.Data;

// ========================================================================== 
public interface IDataFactory
{
  IDataAccess Data();
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
  public abstract IDataAccess Data();
  public abstract void SetupDatabase();
}
