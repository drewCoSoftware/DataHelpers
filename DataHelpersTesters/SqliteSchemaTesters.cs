using System;
using System.IO;
using drewCo.Tools;
using NUnit.Framework;
using DataHelpers.Data;
using System.Collections.Generic;

using IgnoreTest = NUnit.Framework.IgnoreAttribute;
using System.Linq.Expressions;
using DataHelpers;
using System.Linq;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using NUnit.Framework.Interfaces;
using Dapper;
using static Dapper.SqlMapper;
using System.Data;

namespace DataHelpersTesters;

// ==========================================================================   
// NOTE: All of these test cases could be written in a way that we could run each of them against
// multiple SQL flavors.  I am not 100% sure how that would work ATM, but given that we already have
// a way to generate test data for the query generation tests, I think this is very possible.
public class SqliteSchemaTesters : TestBase
{

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// This test case was provided to show that when we select data with related members, the single relations
  /// will be populated and report ids.
  /// NOTE: This might be a good place to look into adding 'many relation' selections as well.
  /// </summary>
  [Test]
  public void CanGetRelatedDataFromSelectQuery()
  {
    Assert.Inconclusive("'CreateParams' feautre no longer exists!");


    //IDataFactory<VacationSchema> factory = CreateTestDataBaseFor<VacationSchema>(CurrentFunctionName());
    //var schema = factory.Schema;



    //var testPlace = new Place()
    //{
    //  Country = "Testistan",
    //  Name = "The Royal Gardens",
    //};
    //factory.Add<Place>(testPlace);

    //Traveler testTraveler = new()
    //{
    //  FavoritePlace = testPlace,
    //  Name = "Test Testington"
    //};
    //factory.Add(testTraveler);

    //// This shows that we can use the new DB mapper thing to get data....
    //factory.Action(dal =>
    //{
    //  var qParams = Helpers.CreateParams("select", new { id = testTraveler.ID });
    //  Traveler t = (dal as SqliteDataAccess<VacationSchema>).TestQuery<Traveler>("SELECT * FROM Travelers WHERE id = @id", qParams).Single();

    //  Assert.That(t.ID, Is.EqualTo(testTraveler.ID));
    //  Assert.That(t.Name, Is.EqualTo(testTraveler.Name));
    //  Assert.That(t.FavoritePlace.ID, Is.Not.EqualTo(0));
    //});


    //// Let's grab us a traveler and see their favorite place!
    //var check = factory.GetById<Traveler>(testTraveler.ID);
    //Assert.That(check, Is.Not.Null);
    //Assert.That(check.FavoritePlace, Is.Not.Null);
    //Assert.That(check.FavoritePlace.ID, Is.EqualTo(testPlace.ID));

    //// Finally, the data of the relation should be null because we haven't explicitly resolved it.
    //Assert.That(check.FavoritePlace.Data, Is.Null);
  }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// This test case was provided to solve a problem where a type in a schema could have multiple relations
  /// to a single dataset.  While this is OK, the code was only adding one related ID property, using the default
  /// naming convention.  The issue is resolved by setting explicit names, but the code should be able to
  /// detect and fail, or at least generate a non-conflicting name.
  /// </summary>
  [Test]
  public void CanResolveAmbiguousRelationIDProperties()
  {
    Assert.Fail("complete this test!  See comment / SoccerClub.Contact for tips/examples.");
  }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// This test case was provided to show that 
  /// </summary>
  [Test]
  public void CanUseOptionalRelation()
  {

    IDataFactory<VacationSchema> factory = CreateTestDataBaseFor<VacationSchema>(CurrentFunctionName());
    var schema = factory.Schema;

    TableDef td = schema.GetTableDef<Traveler>();
    Assert.That(td, Is.Not.Null);

    var opRelation = td.GetColumn("FavoritePlace_ID");
    Assert.That(opRelation, Is.Not.Null);
    Assert.That(opRelation.IsNullable, "This property should be marked as nullable!");


  }

  // --------------------------------------------------------------------------------------------------------------------------
  // Thanks internet!
  // https://stackoverflow.com/questions/2652460/how-to-get-the-name-of-the-current-method-from-code
  /// <summary>
  /// Returns the name of the current function.
  /// </summary>
  /// <returns>The name of the current method, or '&lt;null&gt;' if it is not available.</returns>
  /// <remarks>
  /// Because of possible inlining and compiler optimization, this function may not have reliable results.
  /// Use the 'nameof' operator if you need exact (but not-portable) results.
  /// </remarks>
  [Obsolete("Use version from drewCo.Tools > 1.4.1.0")]
  [MethodImpl(MethodImplOptions.NoInlining)]
  public static string CurrentFunctionName()
  {
    var st = new StackTrace();
    var sf = st.GetFrame(1);

    return sf?.GetMethod()?.Name ?? "<null>";
  }

  // --------------------------------------------------------------------------------------------------------------------------
  [Test]
  public void CanIncludeNullWhenCreatingParametersFromInstance()
  {
    Assert.Inconclusive("'CreateParams' feautre no longer exists!");

    //// var myData = new { Name = "Dave", Number = null
    //const int TEST_NUMBER = 123;
    //var myData = new SimplePerson()
    //{
    //  Name = null,
    //  Number = TEST_NUMBER,
    //};

    //Dictionary<string, object> qParams = Helpers.CreateParams("insert", myData, includeNulls: true);

    //Assert.That(qParams.Count, Is.EqualTo(2), "There should be two parameters!");
    //var number = qParams[nameof(SimplePerson.Number)];
    //Assert.That(number, Is.EqualTo(TEST_NUMBER));

    //var name = qParams[nameof(SimplePerson.Name)];
    //Assert.That(name, Is.Null);

    //// TODO: 
  }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// This test case was provided to show that we can do a many->many mapping from our schema defs.
  /// Of especial importance is that the system is able to auto-generate a mapping table for our use.
  /// </summary>
  [Test]
  public void CanModelManytoManyRelationship()
  {
    IDataFactory<VacationSchema> factory = CreateTestDataBaseFor<VacationSchema>(nameof(CanModelManytoManyRelationship));
    var schema = factory.Schema;


    // Find the mapping table!
    var mappingTable = (from x in schema.TableDefs where x.Name.EndsWith("_map") select x).SingleOrDefault();
    Assert.That(mappingTable, Is.Not.Null, "We should have the mapping table!");

    // We want to make sure that none of the first-class sets in the schema have a relation column.
    // We achieve this with a simple column count....
    var travelersSet = schema.GetTableDef(nameof(VacationSchema.Travelers));
    Assert.That(travelersSet.Columns.Count, Is.EqualTo(3));

    var placesSet = schema.GetTableDef(nameof(VacationSchema.Places));
    Assert.That(placesSet.Columns.Count, Is.EqualTo(3));


    // Make sure that the mapping table points to the other two:
    var toTravelers = mappingTable.GetColumn($"{nameof(VacationSchema.Travelers)}_ID");
    Assert.That(toTravelers, Is.Not.Null);
    Assert.That(toTravelers.RelatedDataSet.TargetSet, Is.SameAs(travelersSet), $"This should be related to the {nameof(VacationSchema.Travelers)} dataset!");


    var toPlaces = mappingTable.GetColumn($"{nameof(VacationSchema.Places)}_ID");
    Assert.That(toPlaces, Is.Not.Null);
    Assert.That(toPlaces.RelatedDataSet.TargetSet, Is.SameAs(placesSet), $"This should be related to the {nameof(VacationSchema.Places)} dataset!");


    // Do some queries, I gues....
    var allPlaces = new List<Place>();
    const int MAX_PLACES = 2;
    for (int i = 0; i < MAX_PLACES; i++)
    {
      var p = new Place()
      {
        Country = "Fakeistan",
        Name = "Destination_" + i
      };
      int id = factory.Add(p);
      allPlaces.Add(p);
    }

    var alltravelers = new List<Traveler>();
    const int MAX_VISITOR = 3;
    for (int i = 0; i < MAX_VISITOR; i++)
    {
      var t = new Traveler()
      {
        Name = "Dave: " + i,
      };
      int id = factory.Add(t);
      alltravelers.Add(t);
    }

    // Now we can associate the people to the places, as we see fit.
    // For ease of writing, we are going to associate all->all.
    var mapTable = schema.GetMappingTable<Place, Traveler>();
    foreach (var place in allPlaces)
    {
      foreach (var traveler in alltravelers)
      {
        string query = mapTable.GetInsertQuery();
        factory.Action(dal =>
        {
          int newId = dal.RunSingleQuery<int>(query, new { Places_ID = place.ID, Travelers_ID = traveler.ID });
          Assert.That(newId, Is.Not.EqualTo(0), "The mapping entry was not added!");
        });
      }
    }

    // At this point we could show that we can pull them back out, but if they are in the DB,
    // then I am sure that we could select them back out if we wanted to....
  }




  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// This test case shows that we can relate a single entity to many.
  /// In this case, we use the BusinessSchema type to show that a single Town can have many Addresses.
  /// </summary>
  [Test]
  public void CanModelOneToManyRelationship()
  {
    var factory = CreateTestDataBaseFor<BusinessSchema>(nameof(CanModelOneToManyRelationship));
    var schema = factory.Schema;

    // TODO: Check to see that the table defs are correct....
    var td = schema.GetTableDef<Address>();
    Assert.That(td.Columns.Count, Is.EqualTo(5));    // One extra property for the FK.

    var col = td.GetColumn("Towns_ID");
    Assert.That(col, Is.Not.Null, "There should be a column for the town relation!");
    var rel = col.RelatedDataSet;
    Assert.That(rel, Is.Not.Null, "The column should have a relation!");
    // Assert.That(rel.data

    var t = new Town()
    {
      Name = "My Town"
    };
    factory.Action(dal =>
    {
      string insert = dal.SchemaDef.GetTableDef<Town>().GetInsertQuery();
      int newId = dal.RunSingleQuery<int>(insert, t);
      t.ID = newId;
    });

    // Let's add some data....
    const int MAX_ADDR = 3;
    for (int i = 0; i < MAX_ADDR; i++)
    {
      var addr = new Address()
      {
        City = "SomeCity",
        State = "NB",
        Street = (123 * (i + 1)) + " Main Street",
        Town = t,
      };

      factory.Action(dal =>
      {
        string insert = dal.SchemaDef.GetTableDef<Address>().GetInsertQuery();
        dal.RunSingleQuery<int>(insert, addr);
      });
    }

    // Now we will get the town with the included addresses back out from the DB:
    string query = "SELECT * FROM Addresses WHERE Towns_ID = @townId";
    factory.Action(dal =>
    {
      var addrs = dal.RunQuery<Address>(query, new { townId = t.ID }).ToList();
      Assert.That(addrs.Count, Is.EqualTo(MAX_ADDR));
    });

  }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Shows that we can model a relationship where one data set gets its data by another (ie: Foreign Key)
  /// </summary>
  [Test]
  public void CanModelSingleRelationship()
  {

    string dir = FileTools.GetLocalDir("test-db");
    var factory = new SqliteDataFactory<BusinessSchema>(dir, nameof(CanModelSingleRelationship));
    FileTools.DeleteExistingFile(factory.DBFilePath);

    factory.SetupDatabase();
    var schema = factory.Schema;

    // Show that we have a relation from people to addresses:
    var td = schema.GetTableDef<Person>();
    var fkCol = td.GetColumn($"{nameof(BusinessSchema.Addresses)}_ID");
    Assert.That(fkCol, Is.Not.Null);

    var relatedSet = fkCol.RelatedDataSet;
    Assert.That(relatedSet, Is.Not.Null, "There should be a relationship to a different data set!");


    var testTown = new Town()
    {
      Name = "My Town",
    };
    factory.Action(dal =>
    {
      var td = dal.SchemaDef.GetTableDef<Town>();
      string query = td.GetInsertQuery();
      int newId = dal.RunSingleQuery<int>(query, testTown);
      testTown.ID = newId;
    });

    // Add some data, and show that we can pull it back out....
    var testAddr = new Address()
    {
      City = "Metropolis",
      Street = "123 Steet Lane",
      State = "VA",
      Town = testTown
    };
    factory.Action(dal =>
    {
      var td = dal.SchemaDef.GetTableDef<Address>();
      string query = td.GetInsertQuery();
      int newId = dal.RunSingleQuery<int>(query, testAddr);
      testAddr.ID = newId;

      Assert.That(newId, Is.Not.EqualTo(0));
    });

    // Add a new person that has the given address:
    var testPerson = new Person()
    {
      Address = testAddr,
      Name = "Test Testington",
      Number = 123
    };
    factory.Action(dal =>
    {
      var td = dal.SchemaDef.GetTableDef<Person>();
      string query = td.GetInsertQuery();

      int newId = dal.RunSingleQuery<int>(query, testPerson);
      testPerson.ID = newId;
      Assert.That(newId, Is.Not.EqualTo(0));
    });

  }


  // --------------------------------------------------------------------------------------------------------------------------
  protected static IDataFactory<TSchema> CreateTestDataBaseFor<TSchema>(string dbName)
  {
    string dir = FileTools.GetLocalDir("test-db");
    var factory = new SqliteDataFactory<TSchema>(dir, dbName);
    FileTools.DeleteExistingFile(factory.DBFilePath);
    factory.SetupDatabase();
    return factory;
  }




  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// This shows that properties with the 'Relation' attribute will automatically have FK relations setup in the schema / defs.
  /// </summary>
  [Test]
  public void CanCreateForeignKeyFromRelation()
  {

    var schemaDef = new SchemaDefinition(new SqliteFlavor(), typeof(BusinessSchema));

    // Show that the relationships are correctly modeled.
    {
      var td = schemaDef.GetTableDef<Person>();

      // One child ref. to 'Addresses'
      Assert.That(td.RelatedDataSets.Count, Is.EqualTo(1));
      Assert.That(td.RelatedDataSets[0].TargetSet.Name == nameof(BusinessSchema.Addresses));
    }

    //{
    //  var td = schemaDef.GetTableDef<ClientAccount>();

    //  // One child ref. to 'Account'
    //  Assert.That(td.RelatedDataSets.Count, Is.EqualTo(1));
    //  Assert.That(td.RelatedDataSets[0].TargetSet.Name == nameof(BusinessSchema.People));
    //}



    // Now show that the FKs are properly added when the queries are created.
    {
      // This should have a single FK that points to a parent set 'Town'
      var td = schemaDef.GetTableDef<Address>();
      string createQuery = td.GetCreateQuery();

      CheckSQL(nameof(CanCreateForeignKeyFromRelation) + "\\Address", createQuery);
    }


    {
      var td = schemaDef.GetTableDef<Person>();
      string createQuery = td.GetCreateQuery();

      CheckSQL(nameof(CanCreateForeignKeyFromRelation) + "\\Person", createQuery);
    }

    //{
    //  // This should have two FKs.  one to the account manager (person), and one 
    //  // so that we can associate multiple client accounts with a single 
    //  var td = schemaDef.GetTableDef<ClientAccount>();
    //  string createQuery = td.GetCreateQuery();

    //  CheckSQL(nameof(CanCreateForeignKeyFromRelation) + "\\ClientAccount", createQuery);
    //}

  }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Show that we can create select queries that are more than just 'select *'
  /// </summary>
  // NOTE: This test is probably flavor agnostic...
  [Test]
  public void CanCreateSelectQueryWithSubsetOfProperties()
  {
    var def = new SchemaDefinition(new SqliteFlavor(), typeof(BusinessSchema));
    var td = def.GetTableDef<Person>();

    string select1 = td.GetSelectQuery<Person>(new Expression<Func<Person, object>>[] { x => x.ID, x => x.Name });

    CheckSQL(nameof(CanCreateSelectQueryWithSubsetOfProperties), select1);
  }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Show that we can create select queries that are more than just 'select *'
  /// </summary>
  // NOTE: This test is probably flavor agnostic...
  [Test]
  public void CanCreateSelectQueryWithCriteria()
  {
    var def = new SchemaDefinition(new SqliteFlavor(), typeof(BusinessSchema));
    var td = def.GetTableDef<Person>();

    string select1 = td.GetSelectQuery<Person>(null, x => x.Name == "dave" && x.Number == 10);

    CheckSQL(nameof(CanCreateSelectQueryWithCriteria), select1);
  }

  // --------------------------------------------------------------------------------------------------------------------------
  [Test]
  public void RunSingleQueryFailsWhenResultSetHasMoreThanOneResult()
  {
    IDataFactory<SimpleSchema> factory = CreateTestDataBaseFor<SimpleSchema>(nameof(RunSingleQueryFailsWhenResultSetHasMoreThanOneResult));
    var schema = factory.Schema;


    const string NAME_1 = "Parent1";
    const string NAME_2 = "Parent2";
    // Add two items:
    {
      var p = new SimplePerson()
      {
        Name = NAME_1
      };
      factory.Add(p);
      Assert.That(1, Is.EqualTo(p.ID));
    }
    {
      var p = new SimplePerson()
      {
        Name = NAME_2
      };
      factory.Add(p);
      Assert.That(2, Is.EqualTo(p.ID));
    }

    const string TEST_QUERY = "SELECT * FROM People";
    Assert.Throws<InvalidOperationException>(() =>
    {
      factory.Action(dal =>
      {
        dal.RunSingleQuery<SimplePerson>(TEST_QUERY, null);
      });
    });

    //Assert.Fail("write some factory code that can select single items!");

    //// Let's do a different one....

    // SimplePerson? p1 = factory.GetDataAccess().RunSingleQuery<SimplePerson>(TEST_QUERY + " WHERE ID = 1", null);
    SimplePerson? p1 = factory.GetById<SimplePerson>(1);
    Assert.That(p1, Is.Not.Null);
    Assert.That(NAME_1, Is.EqualTo(p1!.Name));

    SimplePerson? p2 = factory.GetById<SimplePerson>(2);
    Assert.That(p2, Is.Not.Null);
    Assert.That(NAME_2, Is.EqualTo(p2!.Name));
  }

  // --------------------------------------------------------------------------------------------------------------------------
  [Test]
  public void CanCreateUpdateQuery()
  {
    SchemaDefinition schema = CreateSqliteSchema<BusinessSchema>();

    var tableDef = schema.GetTableDef<Person>()!;
    string update = tableDef.GetUpdateQuery();
    CheckSQL(nameof(CanCreateUpdateQuery), update);
  }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// This test case was provided to show that enum types are valid, and can be used like one would expect.
  /// </summary>
  [IgnoreTest("Not implemented!")]
  public void CanUseEnumInSchema()
  {
    // NOTE: Use 'SimpleSchema' for this.
    Assert.Fail("Please finish this test!");

  }

  // --------------------------------------------------------------------------------------------------------------------------
  protected SqliteDataAccess<T> CreateSqliteDatabase<T>(string dbName, out SchemaDefinition schema)
  {
    string dataDir = Path.Combine("./TestData", "Databases");

    string dbFilePath = Path.GetFullPath(Path.Combine(dataDir, dbName + ".sqlite"));

    var factory = new SqliteDataFactory<T>(dataDir, dbFilePath);
    FileTools.DeleteExistingFile(factory.DBFilePath);
    factory.SetupDatabase();

    schema = factory.Schema;

    var res = factory.GetDataAccess() as SqliteDataAccess<T>;
    return res;
  }


  // --------------------------------------------------------------------------------------------------------------------------
  private static SchemaDefinition CreateSqliteSchema<T>()
  {
    return new SchemaDefinition(new SqliteFlavor(), typeof(T));
  }


  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Shows that we can create select queries from lambda expressions.
  /// </summary>
  [Test]
  public void CanGenerateSelectQueryFromLambdaExpression()
  {
    var schema = new SchemaDefinition(new SqliteFlavor(), typeof(BusinessSchema));
    {
      string selectByIdQuery = schema.GetSelectQuery<Person>(x => x.ID == 1);
      CheckSQL(nameof(CanGenerateSelectQueryFromLambdaExpression), selectByIdQuery);
    }
  }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Shows that it isn't possible to create a schema with a non-primary (IHasPrimary) type that has a child
  /// relationship.
  /// </summary>
  [Test]
  public void CantHaveChildRelationshipOnNonPrimaryKeyType()
  {
    // Complete this test!
    // BONK!
    Assert.Throws<InvalidOperationException>(() =>
    {
      var schema = new SchemaDefinition(new SqliteFlavor(), typeof(SchemaWithNonPrimaryType));
    });
  }



  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Shows that a schema with a circular dependency (parent -> child -> child(parent)) is not valid and will crash.
  /// </summary>
  [IgnoreTest("This test is no longer valid after changing how we do parent / child relationships.")]
  public void CantCreateSchemaWithCircularDependency()
  {
    // BONK!
    Assert.Throws<InvalidOperationException>(() =>
    {
      var schema = new SchemaDefinition(new SqliteFlavor(), typeof(SchemaWithCircularDependency));
    });
  }


  // NOTE: We need some kind of test schema for this.  It can be something from this test library.
  // -------------------------------------------------------------------------------------------------------------------------- 
  /// <summary>
  /// Shows that we can automatically create an insert query for a table.
  /// </summary>
  [Test]
  public void CanCreateInsertQuery()
  {
    var schema = new SchemaDefinition(new SqliteFlavor(), typeof(BusinessSchema));
    TableDef? memberTable = schema.GetTableDef(nameof(BusinessSchema.People));
    Assert.That(memberTable, Is.Not.Null);

    string insertQuery = memberTable!.GetInsertQuery();

    // UPDATE: Use the 'checksql' function!

    CheckSQL(nameof(CanCreateInsertQuery), insertQuery);

    //const string EXPECTED = "INSERT INTO People (name,createdate) VALUES (@Name,@CreateDate) RETURNING id";
    //Assert.That(EXPECTED, Is.EqualTo(insertQuery));
  }
}







// ==========================================================================
public class SchemaWithNonPrimaryType
{
  public TypeWithoutPrimary DataSet1 { get; set; }
  public TypewithRelationToNonPrimary DataSet2 { get; set; }
}

// ==========================================================================
public class TypewithRelationToNonPrimary : IHasPrimary
{
  public int ID { get; set; }
  public int Number { get; set; }

  [RelationAttribute(DataSetName = nameof(SchemaWithNonPrimaryType.DataSet1))]
  public TypeWithoutPrimary Relation { get; set; }
}

// ==========================================================================
/// <summary>
/// A data type without a primary key.  This can't be used in child relationships.
/// </summary>
public class TypeWithoutPrimary
{
  public string Name { get; set; }
}


// ==========================================================================
public class SchemaWithCircularDependency
{
  public List<Parent2> Parents { get; set; } = new List<Parent2>();
  public List<TypeWithInvalidChildRelationship> BadKids { get; set; } = new List<TypeWithInvalidChildRelationship>();
}

// ==========================================================================
public class Parent2 : IHasPrimary
{
  public int ID { get; set; }

  [RelationAttribute]
  public TypeWithInvalidChildRelationship Child { get; set; }
}

// ==========================================================================
public class TypeWithInvalidChildRelationship : IHasPrimary
{
  public int ID { get; set; }
  public int Number { get; set; }

  // NOTE: This child relationship is invalid.
  // We already have this type 'InvalidChild' listed as a child of parent.
  // By attempting to also list 'InvalidParent' as a child, we would create
  // a circular dependency.
  [RelationAttribute]
  public Parent2 InvalidParent { get; set; }
}


