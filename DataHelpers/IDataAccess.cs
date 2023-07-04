namespace DataHelpers.Data;

// ==========================================================================
public interface IDataAccess
{
  SchemaDefinition SchemaDef { get; }
  IEnumerable<T> RunQuery<T>(string query, object? qParams);
  int RunExecute(string query, object? qParams);

  /// <summary>
  /// Runs a query which is expected to return a single result.
  /// If more than one result exists, this will throw an exception.
  /// </summary>
  T? RunSingleQuery<T>(string query, object? parameters);
}
