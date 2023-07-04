


using System;
using System.IO;
using DataHelpers.Data;
using DataHelpersTesters;
using drewCo.Tools;
using Xunit;

// =========================================================================================================================
public class PostgresSchemaTesters
{
  public const string TEST_DB_NAME = "DataHelpersTesters";

  // --------------------------------------------------------------------------------------------------------------------------
  [Fact]
  public void CanCreatePostgresSchema()
  {
    //SchemaDefinition schema = CreatePostgresSchema<ExampleSchema>();

    CreatePostgresDatabase<ExampleSchema>(TEST_DB_NAME, out SchemaDefinition schemaDef);
    Assert.NotNull(schemaDef);
  }




  // --------------------------------------------------------------------------------------------------------------------------
  // This test case was provided as an example fo how to do insert types queries with child/parent
  // data.  This will serve as the basis for future query generation and schema structuring code.
  // NOTE: This code is THE SAME as the code in SqliteSchemaTesters.cs file.  The only difference is
  // that is uses a different 'dal' type.
  // --> Since the goal of this project is to have a generic way to talk to make DB drivers, it would
  // make sense to just write one set of test cases (which are generic) and then run them for all of the DB
  // drivers that we might have.
  [Fact]
  public void CanInsertChildRecordsWithParentID()
  {
    var dal = CreatePostgresDatabase<ExampleSchema>(nameof(CanInsertChildRecordsWithParentID), out SchemaDefinition schema);

    var parent = new ExampleParent()
    {
      CreateDate = DateTimeOffset.Now,
      Name = "Parent1"
    };

    string insertQuery = schema.GetTableDef("Parents")?.GetInsertQuery() ?? string.Empty;
    Assert.NotEmpty(insertQuery);

    // NOTE: We are using 'RunSingleQuery' here so that we can get the returned ID!
    int newID = dal.RunSingleQuery<int>(insertQuery, parent);
    Assert.True(newID != 0);
    
//    Assert.Equal(1, newID);

    // HACK: This should maybe be assinged during insert?
    parent.ID = newID;

    // Confirm that we can get the data back out...
    string select = schema.GetSelectQuery<ExampleParent>(x => x.ID == newID);
    var parentCheck = dal.RunQuery<ExampleParent>(select, new { ID = newID });
    Assert.NotNull(parentCheck);

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
    Assert.NotNull(childCheck);
    Assert.Equal("Child1", childCheck!.Label);

  }



  // --------------------------------------------------------------------------------------------------------------------------
  protected PostgresDataAccess<T> CreatePostgresDatabase<T>(string dbName, out SchemaDefinition schema)
  {
    // string dataDir = Path.Combine("./TestData", "Databases");
    // FileTools.CreateDirectory(dataDir);
    // string dbFilePath = Path.GetFullPath(Path.Combine(dataDir, dbName + ".sqlite"));
    // FileTools.DeleteExistingFile(dbFilePath);
    // NOTE: You will need to setup a localhost DB that has the appropriate creds.
    // Since this is a test DB for features, don't worry about the stupid username and password.
    // Don't put sensitive data on a TEST DB!
    string connectionString = $"Host=localhost;Port=5432;Username=test;Password=abc123;Database=DataHelpersTesters";

    schema = CreatePostgresSchema<T>();
    var dal = new PostgresDataAccess<T>(connectionString);
    dal.SetupDatabase();

    return dal;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  private static SchemaDefinition CreatePostgresSchema<T>()
  {
    // https://www.npgsql.org/doc/connection-string-parameters.html
    return new SchemaDefinition(new PostgresFlavor(), typeof(T));
  }

}