
using System;
using System.IO;
using System.Text.Json;
using DataHelpers.Data;
using DataHelpers.Migrations;
using drewCo.Tools;
using Xunit;
namespace DataHelpersTesters;

public class MigrationTesters
{

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Show that we can generate a migration that will 'CREATE' tables
  /// when no previous migration exists.
  /// </summary>
  [Fact]
  public void CanCreateMigrationForNewSchema()
  {
    var def = new SchemaDefinition(new SqliteFlavor(), typeof(ExampleSchema));
    Assert.Equal(2, def.TableDefs.Count);

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
    Assert.False(File.Exists(path));

    var dal = new SqliteDataAccess<ExampleSchema>(dataDir, DB_NAME);
    mh.ApplyMigration(migration, dal);

    // Does the new DB file exist?
    // TODO: We need a real way to validate that it has the correct schema.
    Assert.True(File.Exists(path));

    // Make sure that we also have migration data available...
    Assert.True(File.Exists(migration.SchemaFilePath));
  }


  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Check the given sql against the current known good sql by test name.
  /// If there is no known good sql (it hasn't been generated) you will need to manually check and approve the code.
  /// </summary>
  /// <returns>
  /// A boolean value indicating that the given sql matches the reference SQL on disk.
  /// A null value is returned if the reference SQL hasn't been approved.
  /// </returns>
  private void CheckSQL(string testName, string sql)
  {
    // NOTE: This is meant to be in the code root so that the example files get included in source control.
    string codeRoot = Path.Combine(FileTools.GetAppDir(), "../../../");
    string testDataDirName = "TestData/SQLOutput";

    string sourceTestDataDir = Path.Combine(codeRoot, testDataDirName);
    string binTestDataDir = Path.Combine(FileTools.GetAppDir(), testDataDirName);

    FileTools.CreateDirectory(sourceTestDataDir);

    string sqlFilePath = Path.Combine(sourceTestDataDir, $"{testName}.json");
    if (File.Exists(sqlFilePath))
    {
      var comp = TestSQLOutput.Load(sqlFilePath);
      if (comp.IsApproved)
      {
        Assert.Equal(sql, comp.SQL);
      }
      else
      {
        Console.WriteLine("The reference SQL is not approved!");
        Assert.True(false);
      }
    }
    else
    {
      var comp = new TestSQLOutput()
      {
        IsApproved = false,
        SQL = sql
      };

      string json = JsonSerializer.Serialize(comp);
      File.WriteAllText(sqlFilePath, json);

      // Fail this test anyway...
      Console.WriteLine("The reference SQL does not exist!");
      Assert.True(false);
    }
  }

}

// ========================================================================== 
public class TestSQLOutput : JsonFile<TestSQLOutput>
{
  public bool IsApproved { get; set; } = false;
  public string SQL { get; set; } = string.Empty;
}
