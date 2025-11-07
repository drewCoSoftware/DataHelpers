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

namespace DataHelpersTesters;

// ==========================================================================   
// NOTE: All of these test cases could be written in a way that we could run each of them against
// multiple SQL flavors.  I am not 100% sure how that would work ATM, but given that we already have
// a way to generate test data for the query generation tests, I think this is very possible.
public class SqliteSchemaTesters : TestBase
{
  // --------------------------------------------------------------------------------------------------------------------------
  [Test]
    public void CanIncludeNullWhenCreatingParametersFromInstance() { 
      Assert.Fail();
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
    Assert.That(travelersSet.Columns.Count, Is.EqualTo(2));

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
        factory.Action(dal => {
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

    {
      var td = schemaDef.GetTableDef<ClientAccount>();

      // One child ref. to 'Account'
      Assert.That(td.RelatedDataSets.Count, Is.EqualTo(1));
      Assert.That(td.RelatedDataSets[0].TargetSet.Name == nameof(BusinessSchema.People));
    }



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

    {
      // This should have two FKs.  one to the account manager (person), and one 
      // so that we can associate multiple client accounts with a single 
      var td = schemaDef.GetTableDef<ClientAccount>();
      string createQuery = td.GetCreateQuery();

      CheckSQL(nameof(CanCreateForeignKeyFromRelation) + "\\ClientAccount", createQuery);
    }

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
      factory.Action(dal => {
        dal.RunSingleQuery<SimplePerson>(TEST_QUERY, null);
      });
    });

    Assert.Fail("write some factory code that can select single items!");

    //// Let's do a different one....
    //SimplePerson? p1 = dal.RunSingleQuery<SimplePerson>(TEST_QUERY + " WHERE ID = 1", null);
    //Assert.That(p1, Is.Not.Null);
    //Assert.That(NAME_1, Is.EqualTo(p1!.Name));

    //SimplePerson? p2 = dal.RunSingleQuery<SimplePerson>(TEST_QUERY + " WHERE ID = 2", null);
    //Assert.That(p2, Is.Not.Null);
    //Assert.That(NAME_2, Is.EqualTo(p2!.Name));
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
  }

  //// --------------------------------------------------------------------------------------------------------------------------
  //// This test case was provided as an example fo how to do insert types queries with child/parent
  //// data.  This will serve as the basis for future query generation and schema structuring code.
  //[Test]
  //public void CanInsertChildRecordsWithParentID()
  //{
  //  var dal = CreateSqliteDatabase<BusinessSchema>(nameof(CanInsertChildRecordsWithParentID), out SchemaDefinition schema);

  //  var parent = new ExampleParent()
  //  {
  //    CreateDate = DateTimeOffset.Now,
  //    Name = "Parent1"
  //  };

  //  string insertQuery = schema.GetTableDef("Parents")?.GetInsertQuery() ?? string.Empty;
  //  Assert.That(insertQuery, Is.Not.Empty);

  //  // NOTE: We are using 'RunSingleQuery' here so that we can get the returned ID!
  //  int newID = dal.RunSingleQuery<int>(insertQuery, parent);
  //  Assert.That(1, Is.EqualTo(newID));

  //  // HACK: This should maybe be assinged during insert?
  //  parent.ID = newID;

  //  // Confirm that we can get the data back out...
  //  string select = schema.GetSelectQuery<ExampleParent>(x => x.ID == newID);
  //  var parentCheck = dal.RunQuery<ExampleParent>(select, new { ID = newID });
  //  Assert.That(parentCheck, Is.Not.Null);

  //  // Now we will insert the child record:
  //  var child = new ExampleChild()
  //  {
  //    Label = "Child1",
  //    Parent = parent
  //  };

  //  string insertChild = schema.GetTableDef<ExampleChild>()?.GetInsertQuery() ?? string.Empty;

  //  // Let's see if we can create an anonymous type that can be used for inserts.....
  //  object paramsObject = schema.GetParamatersObject<ExampleChild>(child);
  //  int childID = dal.RunSingleQuery<int>(insertChild, paramsObject); // new { Label = child.Label, Parents_ID = child.Parent.ID });

  //  string selectChild = schema.GetSelectQuery<ExampleChild>(x => x.ID == childID);
  //  var childCheck = dal.RunSingleQuery<ExampleChild>(selectChild, new { ID = childID });
  //  Assert.That(childCheck, Is.Not.Null);
  //  Assert.That("Child1", Is.EqualTo(childCheck!.Label));

  //}

  // --------------------------------------------------------------------------------------------------------------------------
  protected SqliteDataAccess<T> CreateSqliteDatabase<T>(string dbName, out SchemaDefinition schema)
  {
    string dataDir = Path.Combine("./TestData", "Databases");
    //    FileTools.CreateDirectory(dataDir);

    string dbFilePath = Path.GetFullPath(Path.Combine(dataDir, dbName + ".sqlite"));
    //  FileTools.DeleteExistingFile(dbFilePath);

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

  //// --------------------------------------------------------------------------------------------------------------------------
  //// A simple test case to show that our insert queries for types with parents are generated correctly.
  //[Test]
  //public void CanCreateInsertQueryForTypeWithParentRelationship()
  //{
  //  SchemaDefinition schema = CreateSqliteSchema<BusinessSchema>();

  //  var tableDef = schema.GetTableDef<Person>();
  //  string insert = tableDef.GetInsertQuery();
  //  CheckSQL(nameof(CanCreateInsertQueryForTypeWithParentRelationship), insert);

  //}

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Shows that we can create select queries from lambda expressions.
  /// </summary>
  [Test]
  public void CanGenerateSelectQueryFromLambdaExpression()
  {
    var schema = new SchemaDefinition(new SqliteFlavor(), typeof(BusinessSchema));

    // var p = new ExampleParent()
    // {
    //   CreateDate = DateTimeOffset.Now,
    //   Name = "Parent1"
    // };
    // p.Children.Add(new ExampleChild()
    // {
    //   Label = "Child1"
    // });
    // p.Children.Add(new ExampleChild()
    // {
    //   Label = "Child2"
    // });

    {
      string selectByIdQuery = schema.GetSelectQuery<Person>(x => x.ID == 1);
      string expected = "SELECT * FROM Parents WHERE ID = @ID";
      Assert.That(expected, Is.EqualTo(selectByIdQuery));
    }

    // TEMP: Disable....
    // {
    //   string query = schema.GetInsertUpdateQuery(p);
    //   CheckSQL(nameof(CanGenerateInsertQueryForParentAndChildren) + "_Parent", query);
    // }
    // {
    //   string query = schema.GetInsertUpdateQuery(p.Children[0]);
    //   CheckSQL(nameof(CanGenerateInsertQueryForParentAndChildren) + "_Child0", query);
    // }

    // Now let's see if we can actually insert the data.
    // string TEST_DB_NAME = nameof(CanGenerateInsertQueryForParentAndChildren) + ".sqlite";
    // string dbDir = Path.Combine(FileTools.GetAppDir(), "TestDBs");
    // FileTools.CreateDirectory(dbDir);

    // string dbPath = Path.Combine(dbDir, TEST_DB_NAME);
    // FileTools.DeleteExistingFile(dbPath);

    // var access = new SqliteDataAccess<BusinessSchema>(dbDir, TEST_DB_NAME);

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

  //// --------------------------------------------------------------------------------------------------------------------------
  ///// <summary>
  ///// Shows that we can get a SQL statement that creates a table that references another.
  ///// </summary>
  //[Test]
  //public void CanGetCreateTableQueryWithForeignKey()
  //{
  //  var schema = new SchemaDefinition(new SqliteFlavor(), typeof(BusinessSchema));
  //  Assert.That(4, Is.EqualTo(schema.TableDefs.Count));

  //  // Make sure that we have the correct table names!
  //  var tables = new[] { "Parents", "Kids" };
  //  foreach (var tableName in tables)
  //  {
  //    var t = schema.GetTableDef(tableName);
  //    Assert.That(t, Is.Not.Null);
  //  }

  //  // Make sure that this table def has a column that points to the parent table.
  //  var table = schema.GetTableDef("Kids");
  //  Assert.That(table, Is.Not.Null);
  //  Assert.That(table!.RelatedDataSets.Count, Is.EqualTo(1));
  //  Assert.That(table.RelatedDataSets[0].TargetSet.Name, Is.EqualTo(nameof(BusinessSchema.Parents)));

  //  // NOTE: We don't really have a way to check + validate output SQL at this time.
  //  // It would be rad to have some kind of system that was able to save the current query in a
  //  // file of sorts, which we could then mark as 'OK' or whatever.  IF subsequent output deviated from
  //  // that, then we would have a problem....
  //  // The test case would be indeterminant up until we signed off on the initial query code....
  //  string sql = table!.GetCreateQuery();
  //  Console.WriteLine($"Query is: {sql}");


  //  // Let's make sure that the parents table has the correct number of columns as well...
  //  TableDef? parentTable = schema.GetTableDef(nameof(BusinessSchema.Parents));
  //  Assert.That(parentTable, Is.Not.Null);

  //  // We should only have three columns.  A column for the children doesn't make sense!
  //  Assert.That(3, Is.EqualTo(parentTable!.Columns.Count));

  //}

  // // -------------------------------------------------------------------------------------------------------------------------- 
  // [Test]
  // public void CanGetCreateTableQuery()
  // {
  //   var schema = new SchemaDefinition(new SqliteFlavor(), typeof(BusinessSchema));
  //   string query = schema.GetCreateSQL();

  //   throw new NotImplementedException("Please complete this test!");

  //   int x = 10;
  // }

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

    const string EXPECTED = "INSERT INTO People (name,createdate) VALUES (@Name,@CreateDate) RETURNING id";
    Assert.That(EXPECTED, Is.EqualTo(insertQuery));
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


