
// ==========================================================================
using System;
using System.IO;
using System.Text.Json;
using drewCo.Tools;
using Xunit;

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

      string json = JsonSerializer.Serialize(comp, new JsonSerializerOptions()
      {
          WriteIndented = true
      });
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
