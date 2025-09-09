
// ==========================================================================
using System;
using System.IO;
using System.Text.Json;
using DataHelpers.Data;
using drewCo.Tools;
using NUnit.Framework;

public class TestBase
{
  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Check the given sql against the current known good sql by test name.
  /// If there is no known good sql (it hasn't been generated) you will need to manually check and approve the code.
  /// </summary>
  /// <returns>
  /// A boolean value indicating that the given sql matches the reference SQL on disk.
  /// A null value is returned if the reference SQL hasn't been approved.
  /// </returns>
  protected void CheckSQL(string testName, string sql)
  {
    // NOTE: This is meant to be in the code root so that the example files get included in source control.
    string codeRoot = Path.Combine(FileTools.GetAppDir(), "../../../");
    string testDataDirName = "TestData/SQLOutput";

    string sourceTestDataDir = Path.Combine(codeRoot, testDataDirName);
    string binTestDataDir = Path.Combine(FileTools.GetAppDir(), testDataDirName);

    FileTools.CreateDirectory(sourceTestDataDir);

    string sqlFilePath = Path.Combine(sourceTestDataDir, $"{testName}.json");
    string dir = Path.GetDirectoryName(sqlFilePath);
    FileTools.CreateDirectory(dir);

    if (File.Exists(sqlFilePath))
    {
      var comp = TestSQLOutput.Load(sqlFilePath);

      string srcSql = sql.Replace("\r", string.Empty).Replace("\n", string.Empty);
      string compSql = comp.SQL.Replace("\r", string.Empty).Replace("\n", string.Empty);

      if (comp.IsApproved)
      {
        Assert.That(srcSql, Is.EqualTo(compSql));
      }
      else
      {
        Console.WriteLine("The reference SQL is not approved!");
        Assert.That(false);
      }
    }
    else
    {
      var comp = new TestSQLOutput()
      {
        IsApproved = false,
        SQL = sql
      };

      string json = JsonSerializer.Serialize(comp, new JsonSerializerOptions()
      {
        WriteIndented = true
      });
      File.WriteAllText(sqlFilePath, json);

      // Fail this test anyway...
      // We fail here on purpose so that you, the developer can check the newly generated data.
      Console.WriteLine("The reference SQL does not exist and will be created!");
      Console.WriteLine("It is your responsibility to check and validate the new data!");
      Console.WriteLine("If you approve of the content, add or update the file into the repository.");

      Assert.That(false);
    }
  }

  // --------------------------------------------------------------------------------------------------------------------------
  protected SqliteDataAccess<T> GetDataAccess<T>(string dataDir, string dbFilePath)
  {
    var factory = new SqliteDataFactory<T>(dataDir, dbFilePath);
    SqliteDataAccess<T> res = factory.Action() as SqliteDataAccess<T>;
    return res;
  }
}



// ========================================================================== 
public class TestSQLOutput : JsonFile<TestSQLOutput>
{
  public bool IsApproved { get; set; } = false;
  public string SQL { get; set; } = string.Empty;
}
