using DataHelpers;
using DataHelpers.Data;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

using IgnoreTest = NUnit.Framework.IgnoreAttribute;

namespace DataHelpersTesters
{

  // =========================================================================================================
  public class QueryGenerationTesters : TestBase
  {

    // --------------------------------------------------------------------------------------------------------------------------
    /// <summary>
    /// This test case solves a bug where it wasn't possible to build a correct WHERE clause when some of the parameters were null.
    /// </summary>
    [Test]
    public void CanGenerateWhereClauseWithNulls()
    {

      var qpb = new QueryParamsBuilder(new SqliteFlavor());
      qpb.Add("name", null, typeof(string));

      // Also show that QueryParamsBuilder supports this concept.
      qpb.Add("number", new ValueOrNull<int>(null));

      var where = qpb.GetWhereClause();

      Assert.That(where, Is.EqualTo("WHERE name IS NULL AND number IS NULL"), "Invalid where clause!");
    }

    // --------------------------------------------------------------------------------------------------------------------------
    /// <summary>
    /// Shows that GetBy matches all supplied properties and returns null when no entity matches.
    /// </summary>
    /// <remarks>
    /// CLANKER CODE
    /// </remarks>
    [Test]
    public void CanGetEntityByProperties()
    {
      const string TEST_NAME = nameof(CanGetEntityByProperties);
      IDataFactory<VacationSchema> factory = CreateTestDataBaseFor<VacationSchema>(TEST_NAME);

      var expected = new Place() { Name = "King's Gardens", Country = "Testistan" };
      factory.Add(new Place() { Name = expected.Name, Country = "Otherland" });
      factory.Add(new Place() { Name = "Royal Palace", Country = expected.Country });
      factory.Add(expected);

      var byId = factory.GetBySingle<Place>((nameof(Place.ID), expected.ID));
      Assert.That(byId, Is.Not.Null);
      Assert.That(byId!.Name, Is.EqualTo(expected.Name));
      Assert.That(byId.Country, Is.EqualTo(expected.Country));

      // Each property alone matches another row, so both conditions must be applied.
      var match = factory.GetBySingle<Place>(
        (nameof(Place.Name), expected.Name),
        (nameof(Place.Country), expected.Country));

      Assert.That(match, Is.Not.Null);
      Assert.That(match!.ID, Is.EqualTo(expected.ID));
      Assert.That(match.Name, Is.EqualTo(expected.Name));
      Assert.That(match.Country, Is.EqualTo(expected.Country));

      var missing = factory.GetBy<Place>(
        (nameof(Place.Name), "Royal Palace"),
        (nameof(Place.Country), "Otherland"));

      Assert.That(missing, Is.Empty);
    }

    // --------------------------------------------------------------------------------------------------------------------------
    /// <summary>
    /// Shows that the update entity functionality works as expected.
    /// </summary>
    [Test]
    public void CanUpdateEntity()
    {
      const string TEST_NAME = nameof(CanUpdateEntity);

      var schema = new SchemaDefinition(new SqliteFlavor(), typeof(BusinessSchema));
      IDataFactory<BusinessSchema> factory = CreateTestDataBaseFor<BusinessSchema>(TEST_NAME);

      // Make sure that the associations are correct.....
      var adt = factory.Schema.GetTableDef<Address>();
      var tcol = adt.GetColumnByDataStoreName("towns_id");
      var assoc = tcol.AssociationDef;
      Assert.That(assoc.AssociationType, Is.EqualTo(EAssociationType.Single), "Invalid / polluted association type!");

      var town = new Town()
      {
        Name = "MyTown"
      };
      factory.Add(town);

      var testAddr = new Address()
      {
        Street = "123 Street Lane",
        City = "Town",
        State = "XX",
        Town = town
      };
      factory.Add(testAddr);

      var testPerson = new Person()
      {
        Name = "Person1",
        Number = 123,
        Address = testAddr
      };
      factory.Add<Person>(testPerson);


      // Now we can check + change the person.
      var check = factory.GetById<Person>(testPerson.ID);
      Assert.That(check, Is.Not.Null);
      Assert.That(check.Name, Is.EqualTo("Person1"));
      Assert.That(check.Number, Is.EqualTo(123));

      const string UPDATE_NAME = "NewName";
      const int UPDATE_NUMBER = 456;
      factory.UpdateEntity<Person>(check.ID, (nameof(Person.Name), UPDATE_NAME), (nameof(Person.Number), UPDATE_NUMBER));

      var check2 = factory.GetById<Person>(testPerson.ID);
      Assert.That(check2, Is.Not.Null);
      Assert.That(check2.Name, Is.EqualTo(UPDATE_NAME));
      Assert.That(check2.Number, Is.EqualTo(UPDATE_NUMBER));

    }

    // -------------------------------------------------------------------------------------------------------------------------- 
    /// <summary>
    /// This test case was provided to solve a bug where we weren't generating the correct associations
    /// for a data set when a dataset was associated to another set multiple times.
    /// </summary>
    [Test]
    public void AssociationsAreGeneratedForMultipleLinkedDataSets()
    {
      const string TEST_NAME = nameof(AssociationsAreGeneratedForMultipleLinkedDataSets);

      var factory = CreateTestDataBaseFor<SchemaWithMappedTypes>(TEST_NAME);

      var td = factory.Schema.GetTableDef<Matchup>();
      Assert.That(td.Columns.Count, Is.EqualTo(3), "There should be three columns!");

      // NOTE: A better way to get these names is from the dataset def. directly.
      string[] cols = new[] { "favorite_id", "otherteam_id" };
      foreach (var cName in cols)
      {
        var col = td.GetColumnByDataStoreName(cName);
        Assert.That(col, Is.Not.Null);
        Assert.That(col.AssociationDef, Is.Not.Null, "There should be an associated dataset for this column!");
      }
    }


    // -------------------------------------------------------------------------------------------------------------------------- 
    /// <summary>
    /// This test shows us that our facilities to generate join / mapping table type queries.
    /// </summary>
    [Test]
    [IgnoreTest("This is broken, but maybe we fix it later?")]
    public void CanSelectViaMappingTables()
    {
      const string TEST_NAME = nameof(CanSelectViaMappingTables);

      var schema = new SchemaDefinition(new SqliteFlavor(), typeof(SchemaWithMappedTypes));
      IDataFactory<SchemaWithMappedTypes> factory = CreateTestDataBaseFor<SchemaWithMappedTypes>(TEST_NAME);

      // Show that the mapping table was created and we can reference it:
      var td = factory.Schema.GetMappingTable<Player, Team>();
      Assert.That(td, Is.Not.Null);
      Assert.That(td.IsMappingTable, Is.True);


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
          Players = new ManyAssociation<Player>(from x in players where x.ID <= i select x)
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
