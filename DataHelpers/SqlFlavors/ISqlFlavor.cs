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

//    string GetIdentitySyntax(ColumnDef col);
  }


  // ============================================================================================================================
  public interface IDataTypeResolver
  {
    // NOTE: Sometimes we have to know if we are dealing with a primary key or not.
    string GetDataTypeName(Type t, bool isPrimaryCol);
  }
}
