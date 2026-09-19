using DataHelpers;
using DataHelpers.Data;
using NUnit.Framework;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace DataHelpersTesters
{

  // =========================================================================================================
  public class QueryGenerationTesters : TestBase
  {

    // -------------------------------------------------------------------------------------------------------------------------- 
    /// <summary>
    /// This test case was provided to solve a bug where we weren't generating the correct associations
    /// for a data set when a dataset was associated to another set multiple times.
    /// </summary>
    [Test]
    public void AssociationsAreGeneratedForMultipleLinkedDataSets()
    {
      const string TEST_NAME = nameof(AssociationsAreGeneratedForMultipleLinkedDataSets);

      var schema = new SchemaDefinition(new SqliteFlavor(), typeof(SchemaWithMappedTypes));
      IDataFactory<SchemaWithMappedTypes> factory = CreateTestDataBaseFor<SchemaWithMappedTypes>(TEST_NAME);

      var td = factory.Schema.GetTableDef<Matchup>();
      Assert.That(td.Columns.Count, Is.EqualTo(3), "There should be three columns!");

      string[] cols = new[] { "favorite_ID", "other_ID" };
      foreach (var cName in cols) { 
        var col = td.GetColumn(cName);
        Assert.That(col.AssociatedDataSet, Is.Not.Null, "There should be an associated dataset for this column!");
      }
    }


    // -------------------------------------------------------------------------------------------------------------------------- 
    /// <summary>
    /// This test shows us that our facilities to generate join / mapping table type queries.
    /// </summary>
    [Test]
    public void CanSelectViaMappingTables()
    {
      const string TEST_NAME = nameof(CanSelectViaMappingTables);

      var schema = new SchemaDefinition(new SqliteFlavor(), typeof(SchemaWithMappedTypes));
      IDataFactory<SchemaWithMappedTypes> factory = CreateTestDataBaseFor<SchemaWithMappedTypes>(TEST_NAME);

      // Show that the mapping table was created and we can reference it:
      var td = factory.Schema.GetMappingTable<Player, Team>();
      Assert.That(td, Is.Not.Null);
      Assert.That(td.IsMappingTable, Is.True);


      // column name by the mapped type, like: td.GetMappingColumn<Team>();
      // string colName = td.GetColumn(nameof(Player.Team)).DataStoreName;


      // Let's add some data...
      var players = new List<Player>();
      const int MAX_PLAYERS = 3;
      for (int i = 0; i < MAX_PLAYERS; i++)
      {
        Player p = new Player()
        {
          Name = $"PLAYER_{i + 1}",
          Position = $"position #: {i}"
        };
        factory.Add(p);
        players.Add(p);
      }

      const int MAX_TEAMS = 3;
      for (int i = 0; i < MAX_TEAMS; i++)
      {
        var t = new Team()
        {
          Name = $"TEAM_{i + 1}",

          // Players are added so the teams have 1-2-3 players.
          // This is simply to show some variance.
          Players = new ManyRelation<Player>(from x in players where x.ID <= i select x)
        };
        factory.Add(t);
      }

      // NOTE: We should be able to show that the teams->player mappings are setup automatically
      // since we are using the add function with the players already set.

      // Let's get the list of players for each of the teams.
      // factory.Get<Player>(x => x.ID == 1 && x.Name == "Dave");
      factory.Get<Player>(x => x.ID == 1);

      factory.Get<Player>(x => x.Teams.Prop.ID == 0); // // -- where team.id = x --> how do we easily represent this....

      // This looks at both the player table, and (through) the mapping table.
      factory.Get<Player>(x => x.ID == 1 && x.Teams.Prop.ID == 1);

      // queryparams works, of course, but how do we use expressions...?

      Assert.Inconclusive("please finish this test case!");
    }


    // -------------------------------------------------------------------------------------------------------------------------- 
    /// <summary>
    /// This test case was provided to show that we can leverage some of our newer tech for
    /// building out QueryParameter objects + generating queries with them.  This provides us 
    /// with much easier 'Add (insert)' and other CRUD type functionality.
    /// </summary>
    [Test]
    public void CanDirectInsertObject()
    {
      const string TEST_NAME = nameof(CanDirectInsertObject);

      var schema = new SchemaDefinition(new SqliteFlavor(), typeof(BusinessSchema));
      IDataFactory<BusinessSchema> factory = CreateTestDataBaseFor<BusinessSchema>(TEST_NAME);

      const string TEST_TOWN = "TheTown";
      var town = new Town()
      {
        Name = TEST_TOWN
      };

      int newId = factory.Add(town);
      Assert.That(newId, Is.Not.EqualTo(0));
      Assert.That(newId, Is.EqualTo(town.ID));
    }

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

      // Let's set the hometown association to see if we still get the correct number of params.
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