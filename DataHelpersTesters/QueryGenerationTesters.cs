using DataHelpers;
using DataHelpers.Data;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataHelpersTesters
{

  // =========================================================================================================
  public class QueryGenerationTesters : TestBase
  {

    // --------------------------------------------------------------------------------------------------------------------------
    [Test]
    public void CanCreateInsertQueries()
    {
      const string TEST_NAME = nameof(CanCreateInsertQueries);
      IDataFactory<VacationSchema> factory = CreateTestDataBaseFor<VacationSchema>(TEST_NAME);
      PopulateVacationDB(factory);


      // Show that we can insert a traveler WITH favorite place.
      // The query should include the id column for 'FavoritePlace'
      {
        var ttd = factory.Schema.GetTableDef<Traveler>();
        var t = new Traveler()
        {
          Name = "Perry Mason",
          FavoritePlace = 1
        };
        var qp = ttd.GetInsertQueryFrom(t);
        CheckSQL($"GenerateInsert/WithForeignKey", qp.Query);
      }

      // Show that we can insert a traveler WITH NO favorite place. 
      {
        var ttd = factory.Schema.GetTableDef<Traveler>();
        var t = new Traveler()
        {
          Name = "Kent Golding"
        };
        var qp = ttd.GetInsertQueryFrom(t);
        CheckSQL($"GenerateInsert/WithoutForeignKey", qp.Query);
      }


      var td = factory.Schema.GetTableDef<Place>();

      // Simple test where we can create a basic insert query.
      string q1 = td.GetInsertQuery();
      CheckSQL($"GenerateInsert/Basic", q1);

      // This shows that we can create the insert, and the query params at the same time.
      // This will come in handy later when there are optional (nullable) columns.
      var p = new Place()
      {
        Country = "Monopolia",
        Name = "Marvin Gardens"
      };
      var qp1 = td.GetInsertQueryFrom(p);
      CheckSQL($"GenerateInsert/QueryAndParams", qp1.Query);
    }


    // -------------------------------------------------------------------------------------------------------------------------- 
    /// <summary>
    /// Shows that we can automatically create an insert query for a table.
    /// </summary>
    /// NOTE: This is an older test case, but worth preserving.
    [Test]
    public void CanCreateInsertQuery()
    {
      var schema = new SchemaDefinition(new SqliteFlavor(), typeof(BusinessSchema));
      TableDef? memberTable = schema.GetTableDef(nameof(BusinessSchema.People));
      Assert.That(memberTable, Is.Not.Null);

      string insertQuery = memberTable!.GetInsertQuery();
      CheckSQL(nameof(CanCreateInsertQuery), insertQuery);
    }


  }
}