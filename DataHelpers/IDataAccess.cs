using Dapper;

namespace DataHelpers.Data;

// ==========================================================================
public interface IDataAccess<TSchema> : IDisposable
{
  SchemaDefinition SchemaDef { get; }


  /// <param name="qParams">
  /// Options: Any object instance or a QueryParams instance.  Any object that is
  /// not a QueryParams instance will be converted to one internally.
  /// </param>
  IEnumerable<T> RunQuery<T>(string query, object? qParams = null);

  /// <param name="qParams">
  /// Options: Any object instance or a QueryParams instance.  Any object that is
  /// not a QueryParams instance will be converted to one internally.
  /// </param>
  int RunExecute(string query, object? qParams = null);

  /// <summary>
  /// Runs a query which is expected to return a single result.
  /// If more than one result exists, this will throw an exception.
  /// </summary>
  /// <param name="qParams">
  /// Options: Any object instance or a QueryParams instance.  Any object that is
  /// not a QueryParams instance will be converted to one internally.
  /// </param>
  T? RunSingleQuery<T>(string query, object? qParams = null);

  TableAccess<TSchema> Table(string name);


  /// <summary>
  /// Rollback any currently active transaction.
  /// </summary>
  void Rollback();
}
