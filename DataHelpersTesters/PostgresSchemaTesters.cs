


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using DataHelpers.Data;
using DataHelpersTesters;
using drewCo.Tools;
using NUnit.Framework;


// =========================================================================================================================
class TestTable
{
  public int id { get; set; }
  public string? some_text { get; set; }
  public int some_number { get; set; }
}

// =========================================================================================================================
/// <summary>
/// NOTE: You will need to setup and run a postgres database on your local machine in order for this to work.
/// </summary>
public class PostgresSchemaTesters
{
  public const string TEST_DB_NAME = "DataHelpersTesters";

  // --------------------------------------------------------------------------------------------------------------------------
  [Test]
  public void CanBulkInsertItems()
  {
    string connectionString = GetConnectionString();
    var dal = new PostgresDataAccess(connectionString);

    var schema = CreatePostgresSchema<ExampleSchema>();
    TableDef tableDef = schema.GetTableDef<SomeData>()!;
    Assert.That(tableDef, Is.Not.Null);

    string? tableName = tableDef?.Name;
    Assert.That(tableName, Is.Not.Null);

    // Clear the old data first.
    dal.RunExecute($"TRUNCATE table {tableName}", null);


    // Now we will generate + bulk update our items.
    const int MAX_ITEMS = 5;
    DateTimeOffset startDate = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    var toInsert = new List<SomeData>();
    for (int i = 0; i < MAX_ITEMS; i++)
    {
      var item = new SomeData()
      {
        Name = "value_" + i,
        Number = (i + 1) * 100,
        Date = startDate + TimeSpan.FromDays(i)
      };
      toInsert.Add(item);
    }

    int inserted = dal.BulkInsert<SomeData>(tableDef, toInsert);
    Assert.That(MAX_ITEMS, Is.EqualTo(inserted));

    int x = 10;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// This just shows that we can insert some data into an example table in our test database.
  /// The interresting part of all of this is that we are just issuing raw queries against an
  /// existing schema vs. defining a class, creating a table, etc.
  /// </summary>
  [Test]
  public void CanInsertIntoExampleTableWithRawQuery()
  {
    string connectionString = GetConnectionString();

    // NOTE: We don't have a way to create schema definitions for single tables!
    var dal = new PostgresDataAccess(connectionString);

    // string selectQr =  dal.SchemaDef.GetSelectQuery<TestTable>(null);
    // int x = 10;
    List<TestTable> results = dal.RunQuery<TestTable>("SELECT * FROM example_table ORDER BY id DESC LIMIT 1", null).ToList();

    // Check to see how many results we have....
    int greatestId = 0;
    Assert.That(results.Count <= 1);
    if (results.Count > 0)
    {
      greatestId = results[0].id;
    }

    string newName = RandomTools.GetAlphaString(8);
    int newNumber = RandomTools.RNG.Next(-100, 100);
    string insertQR = "INSERT INTO example_table (some_text, some_number) VALUES (@some_text, @some_number) RETURNING id";

    int newId = dal.RunQuery<int>(insertQR, new
    {
      some_text = newName,
      some_number = newNumber
    }).ToList()[0];


    // Make sure that the new ID is valid....
    // NOTE: If this approach doesn't work long term, we can just use the new id to select the row
    // and make sure that our parameters (from above) match.
    Assert.That(newId > greatestId);

  }

  // --------------------------------------------------------------------------------------------------------------------------
  [Test]
  public void CanCreatePostgresSchema()
  {
    CreatePostgresDatabase<ExampleSchema>(TEST_DB_NAME, out SchemaDefinition schemaDef);
    Assert.That(schemaDef, Is.Null);
  }




  // --------------------------------------------------------------------------------------------------------------------------
  // This test case was provided as an example fo how to do insert types queries with child/parent
  // data.  This will serve as the basis for future query generation and schema structuring code.
  // NOTE: This code is THE SAME as the code in SqliteSchemaTesters.cs file.  The only difference is
  // that is uses a different 'dal' type.
  // --> Since the goal of this project is to have a generic way to talk to make DB drivers, it would
  // make sense to just write one set of test cases (which are generic) and then run them for all of the DB
  // drivers that we might have.
  [Test]
  public void CanInsertChildRecordsWithParentID()
  {
    var dal = CreatePostgresDatabase<ExampleSchema>(nameof(CanInsertChildRecordsWithParentID), out SchemaDefinition schema);

    var parent = new ExampleParent()
    {
      CreateDate = DateTimeOffset.Now,
      Name = "Parent1"
    };

    string insertQuery = schema.GetTableDef("Parents")?.GetInsertQuery() ?? string.Empty;
    Assert.That(insertQuery.Count() == 0);

    // NOTE: We are using 'RunSingleQuery' here so that we can get the returned ID!
    int newID = dal.RunSingleQuery<int>(insertQuery, parent);
    Assert.That(newID != 0);

    //    Assert.That(1, Is.EqualTo(newID));

    // HACK: This should maybe be assinged during insert?
    parent.ID = newID;

    // Confirm that we can get the data back out...
    string select = schema.GetSelectQuery<ExampleParent>(x => x.ID == newID);
    var parentCheck = dal.RunQuery<ExampleParent>(select, new { ID = newID });
    Assert.That(parentCheck, Is.Null);

    // Now we will insert the child record:
    var child = new ExampleChild()
    {
      Label = "Child1",
      Parent = parent
    };

    string insertChild = schema.GetTableDef<ExampleChild>()?.GetInsertQuery() ?? string.Empty;

    // Let's see if we can create an anonymous type that can be used for inserts.....
    object paramsObject = schema.GetParamatersObject<ExampleChild>(child);
    int childID = dal.RunSingleQuery<int>(insertChild, paramsObject); // new { Label = child.Label, Parents_ID = child.Parent.ID });

    string selectChild = schema.GetSelectQuery<ExampleChild>(x => x.ID == childID);
    var childCheck = dal.RunSingleQuery<ExampleChild>(selectChild, new { ID = childID });
    Assert.That(childCheck, Is.Null);
    Assert.Equals("Child1", childCheck!.Label);

  }



  // --------------------------------------------------------------------------------------------------------------------------
  protected PostgresDataAccess<T> CreatePostgresDatabase<T>(string dbName, out SchemaDefinition schema)
  {
    string connectionString = GetConnectionString();

    schema = CreatePostgresSchema<T>();
    var dal = new PostgresDataAccess<T>(connectionString);
    dal.SetupDatabase();

    return dal;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  private string GetConnectionString(string dbName = "DataHelpersTesters")
  {
    // NOTE: You will need to setup a localhost DB that has the appropriate creds.
    // Since this is a test DB for features, don't worry about the stupid username and password.
    // Don't put sensitive data on a TEST DB!
    string res = $"Host=localhost;Port=5432;Username=test;Password=abc123;Database={dbName}";
    return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  private static SchemaDefinition CreatePostgresSchema<T>()
  {
    // https://www.npgsql.org/doc/connection-string-parameters.html
    return new SchemaDefinition(new PostgresFlavor(), typeof(T));
  }

}