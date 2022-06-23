
using System;
using DataHelpers.Data;
using DataHelpers.Migrations;
using Xunit;
namespace DataHelpersTesters;

public class MigrationTesters
{

  /// <summary>
  /// Show that we can generate a migration that will 'CREATE' tables
  /// when no previous migration exists.
  /// </summary>
  [Fact]
  public void CanCreateMigrationForNewSchema()
  {
    var def = new SchemaDefinition(new SqliteFlavor(), typeof(ExampleSchema));
    Assert.Equal(2, def.TableDefs.Count);

    var mh = new MigrationHelper();
    var migration = mh.CreateMigration(null, new DataSchema()
    {
      Version = 1,
      SchemaDef = def,
    });

    Console.WriteLine("The migration script is:");
    Console.WriteLine(migration.Script.SQL);
  }

}