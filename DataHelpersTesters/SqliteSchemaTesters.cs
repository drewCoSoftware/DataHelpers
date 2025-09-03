//OLD:  Probably useless..
using System;
using System.IO;
using drewCo.Tools;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using DataHelpers.Data;
using System.Collections.Generic;
using System.Diagnostics;

using IgnoreTest = NUnit.Framework.IgnoreAttribute;
using System.Linq.Expressions;

namespace DataHelpersTesters;

// ==========================================================================   
// NOTE: All of these test cases could be written in a way that we could run each of them against
// multiple SQL flavors.  I am not 100% sure how that would work ATM, but given that we already have
// a way to generate test data for the query generation tests, I think this is very possible.
public class SqliteSchemaTesters : TestBase
{

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Show that we can create select queries that are more than just 'select *'
  /// </summary>
  [Test]
  public void CanCreateSelectQueryWithSubsetOfProperties()
  {
    var def = new SchemaDefinition(new SqliteFlavor(), typeof(ExampleSchema));
    var td = def.GetTableDef<SomeData>();


    string select1 = td.GetSelectQuery<SomeData>(new Expression<Func<SomeData, object>>[] { x=>x.ID, x=>x.Name });

    CheckSQL(nameof(CanCreateSelectQueryWithSubsetOfProperties), select1);
  }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Show that we can create select queries that are more than just 'select *'
  /// </summary>
  [Test]
  public void CanCreateSelectQueryWithCriteria()
  {
    var def = new SchemaDefinition(new SqliteFlavor(), typeof(ExampleSchema));
    var td = def.GetTableDef<SomeData>();


    string select1 = td.GetSelectQuery<SomeData>(null, x=> x.Name == "dave" && x.Number == 10);

    CheckSQL(nameof(CanCreateSelectQueryWithCriteria), select1);
  }

  // --------------------------------------------------------------------------------------------------------------------------
  [Test]
  public void RunSingleQueryFailsWhenResultSetHasMoreThanOneResult()
  {
    string dbName = nameof(RunSingleQueryFailsWhenResultSetHasMoreThanOneResult);
    SqliteDataAccess<ExampleSchema> dal = CreateSqliteDatabase<ExampleSchema>(dbName, out SchemaDefinition schema);

    const string NAME_1 = "Parent1";
    const string NAME_2 = "Parent2";
    // Add two items:
    {
      ExampleParent p = new ExampleParent()
      {
        CreateDate = DateTime.Now,
        Name = NAME_1
      };
      dal.InsertNew(p);
      Assert.That(1, Is.EqualTo(p.ID));
    }
    {
      ExampleParent p = new ExampleParent()
      {
        CreateDate = DateTime.Now,
        Name = NAME_2
      };
      dal.InsertNew(p);
      Assert.That(2, Is.EqualTo(p.ID));
    }

    const string TEST_QUERY = "SELECT * FROM Parents";
    Assert.Throws<InvalidOperationException>(() =>
    {
      dal.RunSingleQuery<ExampleParent>(TEST_QUERY, null);
    });

    // Let's do a different one....
    ExampleParent? p1 = dal.RunSingleQuery<ExampleParent>(TEST_QUERY + " WHERE ID = 1", null);
    Assert.That(p1, Is.Not.Null);
    Assert.That(NAME_1, Is.EqualTo(p1!.Name));

    ExampleParent? p2 = dal.RunSingleQuery<ExampleParent>(TEST_QUERY + " WHERE ID = 2", null);
    Assert.That(p2, Is.Not.Null);
    Assert.That(NAME_2, Is.EqualTo(p2!.Name));
  }

  // --------------------------------------------------------------------------------------------------------------------------
  [Test]
  public void CanCreateUpdateQuery()
  {
    SchemaDefinition schema = CreateSqliteSchema<ExampleSchema>();

    var tableDef = schema.GetTableDef<ExampleParent>()!;
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



  // --------------------------------------------------------------------------------------------------------------------------
  // This test case was provided as an example fo how to do insert types queries with child/parent
  // data.  This will serve as the basis for future query generation and schema structuring code.
  [Test]
  public void CanInsertChildRecordsWithParentID()
  {
    var dal = CreateSqliteDatabase<ExampleSchema>(nameof(CanInsertChildRecordsWithParentID), out SchemaDefinition schema);

    var parent = new ExampleParent()
    {
      CreateDate = DateTimeOffset.Now,
      Name = "Parent1"
    };

    string insertQuery = schema.GetTableDef("Parents")?.GetInsertQuery() ?? string.Empty;
    Assert.That(insertQuery, Is.Not.Empty);

    // NOTE: We are using 'RunSingleQuery' here so that we can get the returned ID!
    int newID = dal.RunSingleQuery<int>(insertQuery, parent);
    Assert.That(1, Is.EqualTo(newID));

    // HACK: This should maybe be assinged during insert?
    parent.ID = newID;

    // Confirm that we can get the data back out...
    string select = schema.GetSelectQuery<ExampleParent>(x => x.ID == newID);
    var parentCheck = dal.RunQuery<ExampleParent>(select, new { ID = newID });
    Assert.That(parentCheck, Is.Not.Null);

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
    Assert.That(childCheck, Is.Not.Null);
    Assert.That("Child1", Is.EqualTo(childCheck!.Label));

  }

  // --------------------------------------------------------------------------------------------------------------------------
  protected SqliteDataAccess<T> CreateSqliteDatabase<T>(string dbName, out SchemaDefinition schema)
  {
    string dataDir = Path.Combine("./TestData", "Databases");
    FileTools.CreateDirectory(dataDir);

    string dbFilePath = Path.GetFullPath(Path.Combine(dataDir, dbName + ".sqlite"));
    FileTools.DeleteExistingFile(dbFilePath);

    schema = CreateSqliteSchema<T>();
    var dal = new SqliteDataAccess<T>(dataDir, dbName);
    dal.SetupDatabase();

    return dal;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  private static SchemaDefinition CreateSqliteSchema<T>()
  {
    return new SchemaDefinition(new SqliteFlavor(), typeof(T));
  }

  // --------------------------------------------------------------------------------------------------------------------------
  // A simple test case to show that our insert queries for types with parents are generated correctly.
  [Test]
  public void CanCreateInsertQueryForTypeWithParentRelationship()
  {
    SchemaDefinition schema = CreateSqliteSchema<ExampleSchema>();

    var tableDef = schema.GetTableDef<ExampleChild>();
    string insert = tableDef.GetInsertQuery();
    CheckSQL(nameof(CanCreateInsertQueryForTypeWithParentRelationship), insert);

  }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Shows that we can create select queries from lambda expressions.
  /// </summary>
  [Test]
  public void CanGenerateSelectQueryFromLambdaExpression()
  {
    var schema = new SchemaDefinition(new SqliteFlavor(), typeof(ExampleSchema));

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
      string selectByIdQuery = schema.GetSelectQuery<ExampleParent>(x => x.ID == 1);
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

    // var access = new SqliteDataAccess<ExampleSchema>(dbDir, TEST_DB_NAME);

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
      var schema = new SchemaDefinition(new SqliteFlavor(), typeof(SchemaWithNonPrimaryParentType));
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

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Shows that we can get a SQL statement that creates a table that references another.
  /// </summary>
  [Test]
  public void CanGetCreateTableQueryWithForeignKey()
  {
    var schema = new SchemaDefinition(new SqliteFlavor(), typeof(ExampleSchema));
    Assert.That(3, Is.EqualTo(schema.TableDefs.Count));

    // Make sure that we have the correct table names!
    var tables = new[] { "Parents", "Kids" };
    foreach (var tableName in tables)
    {
      var t = schema.GetTableDef(tableName);
      Assert.That(t, Is.Not.Null);
    }

    // Make sure that this table def has a column that points to the parent table.
    var table = schema.GetTableDef("Kids");
    Assert.That(table, Is.Not.Null);
    Assert.That(table!.ParentTables.Count, Is.EqualTo(1));
    Assert.That(table.ParentTables[0].Def.Name, Is.EqualTo(nameof(ExampleSchema.Parents)));

    // NOTE: We don't really have a way to check + validate output SQL at this time.
    // It would be rad to have some kind of system that was able to save the current query in a
    // file of sorts, which we could then mark as 'OK' or whatever.  IF subsequent output deviated from
    // that, then we would have a problem....
    // The test case would be indeterminant up until we signed off on the initial query code....
    string sql = table!.GetCreateQuery();
    Console.WriteLine($"Query is: {sql}");


    // Let's make sure that the parents table has the correct number of columns as well...
    TableDef? parentTable = schema.GetTableDef(nameof(ExampleSchema.Parents));
    Assert.That(parentTable, Is.Not.Null);

    // We should only have three columns.  A column for the children doesn't make sense!
    Assert.That(3, Is.EqualTo(parentTable!.Columns.Count));

  }

  // // -------------------------------------------------------------------------------------------------------------------------- 
  // [Test]
  // public void CanGetCreateTableQuery()
  // {
  //   var schema = new SchemaDefinition(new SqliteFlavor(), typeof(ExampleSchema));
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
    var schema = new SchemaDefinition(new SqliteFlavor(), typeof(ExampleSchema));
    TableDef? memberTable = schema.GetTableDef(nameof(ExampleSchema.Parents));
    Assert.That(memberTable, Is.Not.Null);

    string insertQuery = memberTable!.GetInsertQuery();

    const string EXPECTED = "INSERT INTO Parents (name,createdate) VALUES (@Name,@CreateDate) RETURNING id";
    Assert.That(EXPECTED, Is.EqualTo(insertQuery));
  }
}







// ==========================================================================
public class SchemaWithNonPrimaryParentType
{
  public NonPrimaryParent Parent { get; set; }
  public ExampleChild Child { get; set; }
}

// ==========================================================================
/// <summary>
/// A data type without a primary key.  This can't be used in child relationships.
/// </summary>
public class NonPrimaryParent
{
  [ChildRelationship]
  public ExampleChild SomeKid { get; set; }
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

  [ChildRelationship]
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
  [ChildRelationship]
  public Parent2 InvalidParent { get; set; }
}


