namespace DataHelpers.Data
{

  // ============================================================================================================================
  /// <summary>
  /// Interface to help us deal with the difference between different SQL languages.
  /// Ideally we want a single API in our applications so that we can swap data providers on the fly.
  /// </summary>
  public interface ISqlFlavor
  {
    IDataTypeResolver TypeResolver { get; }
  }


  // ============================================================================================================================
  public interface IDataTypeResolver
  {
    string GetDataTypeName(Type t);
//    Type GetRuntimeType(string dataTypeName);
  }
}
