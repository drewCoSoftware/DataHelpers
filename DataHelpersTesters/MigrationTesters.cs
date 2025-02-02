
using System;
using System.IO;
using System.Text.Json;
using DataHelpers.Data;
using DataHelpers.Migrations;
using drewCo.Tools;

using NUnit.Framework;

namespace DataHelpersTesters;

public class MigrationTesters : TestBase
{

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Show that we can generate a migration that will 'CREATE' tables
  /// when no previous migration exists.
  /// </summary>
  [Test]  
  public void CanCreateMigrationForNewSchema()
  {
    var def = new SchemaDefinition(new SqliteFlavor(), typeof(ExampleSchema));
    Assert.That(3, Is.EqualTo(def.TableDefs.Count));

    const string MIGRATION_OUTPUT_DIR = "./Migrations";
    string outputDir = Path.GetFullPath(MIGRATION_OUTPUT_DIR);

    var mh = new MigrationHelper();
    var migration = mh.CreateMigration(null, new DataSchema()
    {
      Flavor = "SQLite",
      Version = 1,
      SchemaDef = def
    }, outputDir);

    CheckSQL(nameof(CanCreateMigrationForNewSchema), migration.Script.SQL);

    const string DB_NAME = nameof(CanCreateMigrationForNewSchema);
    string dataDir = Path.Combine(FileTools.GetAppDir(), "TestData", nameof(CanCreateMigrationForNewSchema));
    FileTools.CreateDirectory(dataDir);

    string path = Path.Combine(dataDir, DB_NAME + ".sqlite");
    FileTools.DeleteExistingFile(path);

    Assert.That(!File.Exists(path));

    var dal = new SqliteDataAccess<ExampleSchema>(dataDir, DB_NAME);
    mh.ApplyMigration(migration, dal);

    // Does the new DB file exist?
    // TODO: We need a real way to validate that it has the correct schema.
    Assert.That(File.Exists(path));

    // Make sure that we also have migration data available...
    Assert.That(File.Exists(migration.SchemaFilePath));
  }

}
