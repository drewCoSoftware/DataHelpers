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
    public void CanCreateQueryParamsFromObjectInstance()
    {
      const string TEST_NAME = nameof(CanCreateQueryParamsFromObjectInstance);

      var schema = new SchemaDefinition(new SqliteFlavor(), typeof(BusinessSchema));
      IDataFactory<BusinessSchema> factory = CreateTestDataBaseFor<BusinessSchema>(TEST_NAME);

      var town = new Town()
      {
        Name = "BigTown",
      };
      factory.Add(town);
      var addr = new Address()
      {
        Street = "123 Street",
        State = "VA",
        City = "Bigtown",
        Town = town
      };
      int addrId = factory.Add(addr);

      var p1 = new Person()
      {
        Name = "Dave",
        Number = 123,
        Address = addr,
      };


      // Now we can create the paramters object...
      {
        var qParams = schema.ComputeParametersFor(p1);
        Assert.That(qParams.Count, Is.EqualTo(3), "Invalid number of parameters! [1]");

        // Check some parameter names to be sure that we are using the ones that are compatible
        // with what we would see in a query.....
        Assert.That(qParams.ContainsKey("Name"));
      }

      // Let's set the hometown relation to see if we still get the correct number of params.
      {
        p1.HomeTown = town;
        var qParams = schema.ComputeParametersFor(p1);
        Assert.That(qParams.Count, Is.EqualTo(4), "Invalid number of parameters! [2]");
      }

    }

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