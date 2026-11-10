using DataHelpers;
using DataHelpers.Data;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace DataHelpersTesters;

public class UniqueAssociationTesters
{
  /// <summary>
  /// Verifies that [Unique] on Single associations behaves like [Unique] on scalar members:
  /// standalone values must be unique, while Group requires only the association/scalar pair
  /// to be unique. Covers rejection of duplicates for both forms and acceptance of distinct pairs.
  /// </summary>
  [TestCase("INSERT INTO owners (id, number, exclusivetarget_id, groupedtarget_id) VALUES (4, 3, 1, 1)")]
  [TestCase("INSERT INTO owners (id, number, exclusivetarget_id, groupedtarget_id) VALUES (4, 1, 4, 1)")]
  public void SingleAssociationsEnforceUniqueIndexes(string duplicateSql)
  {
    var flavor = new SqliteFlavor();
    var schema = new SchemaDefinition(flavor, typeof(UniqueAssociationSchema));
    using var connection = CreateDatabase(schema);

    Execute(connection, "INSERT INTO targets VALUES (1), (2), (3), (4)");

    Execute(connection, "INSERT INTO owners (id, number, exclusivetarget_id, groupedtarget_id) VALUES (1, 1, 1, 1), (2, 2, 2, 1), (3, 1, 3, 2)");

    var exception = Assert.Throws<SqliteException>(() => Execute(connection, duplicateSql));
    Assert.That(exception!.SqliteExtendedErrorCode == 2067, "Expected a unique constraint violation.");
  }

  /// <summary>
  /// Verifies that [Unique] on Many associations behaves like [Unique] on scalar members:
  /// standalone values must be unique, while Group requires only the association/scalar pair
  /// to be unique. Covers rejection of duplicates for both forms and acceptance of distinct pairs.
  /// </summary>
  [TestCase("INSERT INTO children (id, number, exclusiveowner_id, groupedowner_id) VALUES (4, 3, 1, 1)")]
  [TestCase("INSERT INTO children (id, number, exclusiveowner_id, groupedowner_id) VALUES (4, 1, 4, 1)")]
  public void ManyAssociationsEnforceUniqueIndexes(string duplicateSql)
  {
    var flavor = new SqliteFlavor();
    var schema = new SchemaDefinition(flavor, typeof(UniqueAssociationSchema));
    using var connection = CreateDatabase(schema);

    Execute(connection, "INSERT INTO targets VALUES (1), (2), (3), (4)");
    Execute(connection, "INSERT INTO owners (id, number, exclusivetarget_id, groupedtarget_id) VALUES (1, 1, 1, 1), (2, 2, 2, 1), (3, 3, 3, 1), (4, 4, 4, 1)");

    Execute(connection, "INSERT INTO children (id, number, exclusiveowner_id, groupedowner_id) VALUES (1, 1, 1, 1), (2, 2, 2, 1), (3, 1, 3, 2)");

    var exception = Assert.Throws<SqliteException>(() => Execute(connection, duplicateSql));
    Assert.That(exception!.SqliteExtendedErrorCode == 2067, "Expected a unique constraint violation.");
  }

  private static SqliteConnection CreateDatabase(SchemaDefinition schema)
  {
    var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
    connection.Open();

    string sql = schema.GetCreateSQL();
    Execute(connection, sql);

    return connection;
  }

  private static void Execute(SqliteConnection connection, string sql)
  {
    using var command = connection.CreateCommand();
    command.CommandText = sql;
    command.ExecuteNonQuery();
  }
}
