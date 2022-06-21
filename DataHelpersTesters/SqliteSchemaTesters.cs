//OLD:  Probably useless..
using System;
using System.IO;
using drewCo.Tools;
using Microsoft.Data.Sqlite;
using Xunit;
using DataHelpers.Data;
using System.Collections.Generic;
using System.Diagnostics;

namespace DataHelpersTesters
{

  // ==========================================================================   
  public class SqliteSchemaTesters
  {

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
  class ExampleParent : IHasPrimary
  {
    public int ID { get; set; }
    public string Name { get; set; }
    public DateTimeOffset CreateDate { get; set; }

    // This parent has many different children.
    // That means that each child will have an FK that refers back to this specific parent.
    // The parent table does NOT point to its children directly.
    [ChildRelationship]
    public List<ExampleChild> Children { get; set; } = new List<ExampleChild>();
  }

  // ==========================================================================   
  class ExampleSchema
  {
    // NOTE: In the schema, these lists represent tables of data.
    // Could a schema have a single entry table represented as well?
    // It could be useful to have a different generic type to represent a set<T> vs.
    // a list?
    // --> NOTE: We are using these schema types as a declaration of the overall data,
    // not as a means to resolve that data!
    public List<ExampleParent> Parents { get; set; } = new List<ExampleParent>();
    public List<ExampleChild> Kids { get; set; } = new List<ExampleChild>();
  }


  // ==========================================================================
  public class ExampleChild
  {
    public string? Label { get; set; }
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





}


