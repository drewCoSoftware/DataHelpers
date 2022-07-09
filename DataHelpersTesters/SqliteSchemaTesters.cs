//OLD:  Probably useless..
using System;
using System.IO;
using drewCo.Tools;
using Microsoft.Data.Sqlite;
using Xunit;
using DataHelpers.Data;
using System.Collections.Generic;
using System.Diagnostics;

namespace DataHelpersTesters;

// ==========================================================================   
// NOTE: All of these test cases could be written in a way that we could run each of them against
// multiple SQL flavors.  I am not 100% sure how that would work ATM, but given that we already have
// a way to generate test data for the query generation tests, I think this is very possible.
public class SqliteSchemaTesters : TestBase
{


  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Shows that we can create select queries from lambda expressions.
  /// </summary>
  [Fact]
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
        string selectByIdQuery = schema.GetSelectQuery<ExampleParent>(x=>x.ID == 1);
        string expected = "SELECT * FROM Parents WHERE ID = 1";
        Assert.Equal(expected, selectByIdQuery);
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
  [Fact]
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
  [Fact]
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
  [Fact]
  public void CanGetCreateTableQueryWithForeignKey()
  {
    var schema = new SchemaDefinition(new SqliteFlavor(), typeof(ExampleSchema));
    Assert.Equal(2, schema.TableDefs.Count);

    // Make sure that we have the correct table names!
    var tables = new[] { "Parents", "Kids" };
    foreach (var tableName in tables)
    {
      var t = schema.GetTableDef(tableName);
      Assert.NotNull(t);
    }

    // Make sure that this table def has a column that points to the parent table.
    var table = schema.GetTableDef("Kids");
    Assert.NotNull(table);
    Assert.Single(table!.ParentTables);
    Assert.Equal(table.ParentTables[0].Def.Name, nameof(ExampleSchema.Parents));

    // NOTE: We don't really have a way to check + validate output SQL at this time.
    // It would be rad to have some kind of system that was able to save the current query in a
    // file of sorts, which we could then mark as 'OK' or whatever.  IF subsequent output deviated from
    // that, then we would have a problem....
    // The test case would be indeterminant up until we signed off on the initial query code....
    string sql = table!.GetCreateQuery();
    Console.WriteLine($"Query is: {sql}");


    // Let's make sure that the parents table has the correct number of columns as well...
    TableDef? parentTable = schema.GetTableDef(nameof(ExampleSchema.Parents));
    Assert.NotNull(parentTable);

    // We should only have three columns.  A column for the children doesn't make sense!
    Assert.Equal(3, parentTable!.Columns.Count);

  }

  // // -------------------------------------------------------------------------------------------------------------------------- 
  // [Fact]
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
  [Fact]
  public void CanCreateInsertQuery()
  {
    var schema = new SchemaDefinition(new SqliteFlavor(), typeof(ExampleSchema));
    TableDef? memberTable = schema.GetTableDef(nameof(ExampleSchema.Parents));
    Assert.NotNull(memberTable);

    string insertQuery = memberTable!.GetInsertQuery();

    const string EXPECTED = "INSERT INTO Parents (name,createdate) VALUES (@Name,@CreateDate) RETURNING id";
    Assert.Equal(EXPECTED, insertQuery);
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


