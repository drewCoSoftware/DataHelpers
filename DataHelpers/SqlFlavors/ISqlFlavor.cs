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

    /// <summary>
    /// Compute the name that will be used on the data store (typically sql)
    /// for this property.
    /// </summary>
    string GetDataStoreName(string propName)
    {
      string res = propName.ToLower();
      return res;
    }
  }


  // ============================================================================================================================
  public interface IDataTypeResolver
  {
    // NOTE: Sometimes we have to know if we are dealing with a primary key or not.
    string GetDataTypeName(Type t, bool isPrimaryCol);
  }
}
