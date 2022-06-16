//OLD:  Probably useless..
using System;
using System.IO;
using drewCo.Tools;
using Microsoft.Data.Sqlite;
using Xunit;
using DataHelpers.Data;
using System.Collections.Generic;

namespace DataHelpersTesters
{

  // ==========================================================================   
  class ExampleData : IHasPrimary
  {
    public int ID { get; set; }
    public string Name { get; set; }
    public DateTimeOffset CreateDate { get; set; }
  }

  // ==========================================================================   
  class ExampleSchema
  {
    public List<ExampleData> People { get; set; }
  }


  public class SchemaTesters
  {

    // NOTE: We need some kind of test schema for this.  It can be something from this test library.
    // -------------------------------------------------------------------------------------------------------------------------- 
    /// <summary>
    /// Shows that we can automatically create an insert query for a table.
    /// </summary>
    [Fact]
    public void CanCreateInsertQuery()
    {
      var schema = new SchemaDefinition(new SqliteFlavor(), typeof(ExampleSchema));
      TableDef memberTable = schema.TableDefs[0];

      string insertQuery = memberTable.GetInsertQuery();

      const string EXPECTED = "INSERT INTO People (name,createdate) VALUES (@Name,@CreateDate) RETURNING id";
      Assert.Equal(EXPECTED, insertQuery);
    }
  }


}


