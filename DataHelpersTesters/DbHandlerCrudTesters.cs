using System;
using System.Collections.Generic;
using System.Linq;
using DataHelpers;
using DataHelpers.Data;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace DataHelpersTesters;

public class DbHandlerCrudTesters : TestBase
{
  public class NullableSchema
  {
    public List<NullablePerson> People { get; set; } = new List<NullablePerson>();
  }

  public class NullablePerson : IHasPrimary
  {
    public int ID { get; set; }
    public string? Name { get; set; }
    public int Number { get; set; }
  }

  private IDataFactory<SimpleSchema> CreateFactory()
  {
    return CreateTestDataBaseFor<SimpleSchema>($"Crud_{TestContext.CurrentContext.Test.ID}");
  }

  [Test]
  public void EmptyQueriesReturnEmptyCollectionsDefaultsAndZeroCount()
  {
    CreateFactory().Action(db =>
    {
      Assert.That(db.GetAll<SimplePerson>(), Is.Empty);
      Assert.That(db.GetCount<SimplePerson>(), Is.Zero);
      Assert.That(db.GetById<SimplePerson>(123), Is.Null);
      Assert.That(db.GetBy<SimplePerson>(("Name", "Missing")), Is.Empty);
      Assert.That(db.GetBySingle<SimplePerson>(("Name", "Missing")), Is.Null);
      Assert.That(db.RunSingleQuery<SimplePerson>("SELECT * FROM People"), Is.Null);
      Assert.That(db.RunQuery<SimplePerson>("SELECT * FROM People"), Is.Empty);
    });
  }

  [Test]
  public void AddAssignsIdAndDeleteOnlyRemovesTheSelectedRow()
  {
    CreateFactory().Action(db =>
    {
      var first = new SimplePerson { Name = "First", Number = 1 };
      var second = new SimplePerson { Name = "Second", Number = 2 };
      int id = db.Add(first);
      db.Add(second);

      Assert.That(id, Is.GreaterThan(0));
      Assert.That(first.ID, Is.EqualTo(id));
      Assert.That(second.ID, Is.Not.EqualTo(id));
      Assert.That(db.GetById<SimplePerson>(id)!.Number, Is.EqualTo(1));

      Assert.That(db.DeleteById<SimplePerson>(id), Is.EqualTo(1));
      Assert.That(db.DeleteById<SimplePerson>(id), Is.Zero);
      Assert.That(db.GetById<SimplePerson>(id), Is.Null);
      Assert.That(db.GetAll<SimplePerson>().Single().ID, Is.EqualTo(second.ID));
    });
  }

  [Test]
  public void RawQueryHelpersAcceptObjectsAndQueryParams()
  {
    CreateFactory().Action(db =>
    {
      db.Add(new SimplePerson { Name = "O'Brien", Number = 7 });
      db.Add(new SimplePerson { Name = "Other", Number = 8 });

      var parameters = new QueryParamsBuilder(db.Schema.Flavor);
      parameters.Add("name", "O'Brien");

      Assert.That(db.RunQuery<SimplePerson>("SELECT * FROM People WHERE Name = @name", parameters.Build()).Single().Number, Is.EqualTo(7));
      Assert.That(db.RunSingleQuery<SimplePerson>("SELECT * FROM People WHERE Number = @number", new { number = 8 })!.Name, Is.EqualTo("Other"));
      Assert.That(db.GetCount<SimplePerson>("Number >= @minimum", new { minimum = 7 }), Is.EqualTo(2));

      int changed = db.RunExecute("UPDATE People SET Number = @number WHERE Name = @name", new { number = 9, name = "O'Brien" });

      Assert.That(changed, Is.EqualTo(1));
      Assert.That(db.GetBySingle<SimplePerson>(("Name", "O'Brien"))!.Number, Is.EqualTo(9));
      Assert.That(db.RunExecute("DELETE FROM People WHERE Number = @number", new { number = -1 }), Is.Zero);

      var updateParameters = new QueryParamsBuilder(db.Schema.Flavor);
      updateParameters.Add("name", "Other");
      updateParameters.Add("number", 10);

      Assert.That(db.RunExecute("UPDATE People SET Number = @number WHERE Name = @name", updateParameters.Build()), Is.EqualTo(1));
      Assert.That(db.GetCount<SimplePerson>("Number = @number", updateParameters.Build()), Is.EqualTo(1));
    });
  }

  [Test]
  public void TupleFiltersUseAllPropertiesForBothOverloads()
  {
    CreateFactory().Action(db =>
    {
      db.Add(new SimplePerson { Name = "Match", Number = 1 });
      db.Add(new SimplePerson { Name = "Match", Number = 2 });
      db.Add(new SimplePerson { Name = "Other", Number = 1 });

      var filters = new List<(string, object)> { ("Name", "Match"), ("Number", 2) };

      Assert.That(db.GetBy<SimplePerson>(filters).Single().Number, Is.EqualTo(2));
      Assert.That(db.GetBy<SimplePerson>(("Name", "Match"), ("Number", 1)).Single().Number, Is.EqualTo(1));
      Assert.That(db.GetBySingle<SimplePerson>(("Name", "Other"), ("Number", 1))!.Name, Is.EqualTo("Other"));
    });
  }

  [TestCase(1, new[] { 4, 3 })]
  [TestCase(2, new[] { 2, 1 })]
  [TestCase(3, new int[0])]
  public void ExampleQueriesApplyFiltersOrderingAndPageOffset(int page, int[] expected)
  {
    CreateFactory().Action(db =>
    {
      for (int number = 1; number <= 4; number++)
      {
        db.Add(new SimplePerson { Name = "Match", Number = number });
      }

      db.Add(new SimplePerson { Name = "Other", Number = 99 });

      var rows = db.GetBy<SimplePerson>(new { Name = "Match" }, "Number DESC", page, 2);

      Assert.That(rows.Select(row => row.Number), Is.EqualTo(expected));
    });
  }

  [Test]
  public void ExampleProjectionSelectsOnlyRequestedColumns()
  {
    CreateFactory().Action(db =>
    {
      db.Add(new SimplePerson { Name = "Match", Number = 42 });
      db.Add(new SimplePerson { Name = "Other", Number = 42 });

      var row = db.GetBy<SimplePerson>(new { Name = "Match", Number = 42 }, new[] { "Name", "Number" }).Single();

      Assert.That(row.Name, Is.EqualTo("Match"));
      Assert.That(row.Number, Is.EqualTo(42));
      Assert.That(row.ID, Is.Zero);
    });
  }

  [TestCase(false)]
  [TestCase(true)]
  public void UpdateChangesRequestedColumnsAndIgnoresSuppliedPrimaryKey(bool useEditData)
  {
    CreateFactory().Action(db =>
    {
      var person = new SimplePerson { Name = "Before", Number = 42 };
      db.Add(person);

      if (useEditData)
      {
        var edits = EditData.Create<SimplePerson>();
        edits.AddProperty("ID", typeof(int), 999);
        edits.AddProperty("Name", typeof(string), "After");

        db.UpdateEntity<SimplePerson>(person.ID, edits);
      }
      else
      {
        db.UpdateEntity<SimplePerson>(person.ID, ("ID", 999), ("Name", "After"));
      }

      var updated = db.GetById<SimplePerson>(person.ID)!;

      Assert.That(updated.Name, Is.EqualTo("After"));
      Assert.That(updated.Number, Is.EqualTo(42));
      Assert.That(updated.ID, Is.EqualTo(person.ID));
      Assert.That(db.GetById<SimplePerson>(999), Is.Null);
    });
  }

  [TestCase(false)]
  [TestCase(true)]
  public void UpdateCanSetNullableColumnToNullAndQueryIt(bool useEditData)
  {
    CreateTestDataBaseFor<NullableSchema>($"Crud_Nullable_{TestContext.CurrentContext.Test.ID}").Action(db =>
    {
      var person = new NullablePerson { Name = "Before", Number = 42 };
      db.Add(person);

      if (useEditData)
      {
        var edits = EditData.Create<NullablePerson>();
        edits.AddProperty("Name", typeof(string), null);

        db.UpdateEntity<NullablePerson>(person.ID, edits);
      }
      else
      {
        db.UpdateEntity<NullablePerson>(person.ID, ("Name", null));
      }

      Assert.That(db.GetById<NullablePerson>(person.ID)!.Name, Is.Null);
      Assert.That(db.GetBySingle<NullablePerson>(("Name", new ValueOrNull<string>(null)))!.ID, Is.EqualTo(person.ID));
      Assert.That(db.GetBy<NullablePerson>(("Name", new ValueOrNull<string>(null))).Single().Number, Is.EqualTo(42));
    });
  }

  [TestCase(false)]
  [TestCase(true)]
  public void AssociatedInsertsShareCommitAndRollback(bool rollback)
  {
    var factory = CreateTestDataBaseFor<BusinessSchema>($"Crud_Association_{TestContext.CurrentContext.Test.ID}");
    var address = new Address
    {
      Street = "123 Lane",
      City = "Town",
      State = "VA",
      Town = new Town { Name = "Town" }
    };

    factory.Transaction(db =>
    {
      db.Add(address);

      Assert.That(address.ID, Is.GreaterThan(0));
      Assert.That(address.Town.ID, Is.GreaterThan(0));
      Assert.That(db.GetById<Address>(address.ID)!.Town.ID, Is.EqualTo(address.Town.ID));
      Assert.That(db.GetCount<Address>(), Is.EqualTo(1));
      Assert.That(db.GetCount<Town>(), Is.EqualTo(1));

      if (rollback)
      {
        db.Rollback();
      }
    });

    Assert.That(factory.GetCount<Address>(), Is.EqualTo(rollback ? 0 : 1));
    Assert.That(factory.GetCount<Town>(), Is.EqualTo(rollback ? 0 : 1));
  }

  [Test]
  public void AddReusesAlreadySavedAssociatedEntity()
  {
    var factory = CreateTestDataBaseFor<BusinessSchema>($"Crud_Association_{TestContext.CurrentContext.Test.ID}");

    factory.Action(db =>
    {
      var town = new Town { Name = "Existing town" };
      db.Add(town);

      var address = new Address { Street = "123 Lane", City = "Town", State = "VA", Town = town };
      db.Add(address);

      Assert.That(db.GetCount<Town>(), Is.EqualTo(1));
      Assert.That(db.GetById<Address>(address.ID)!.Town.ID, Is.EqualTo(town.ID));
    });
  }

  [Test]
  public void SingleResultHelpersRejectMultipleMatches()
  {
    CreateFactory().Action(db =>
    {
      db.Add(new SimplePerson { Name = "Duplicate" });
      db.Add(new SimplePerson { Name = "Duplicate" });

      Assert.Throws<InvalidOperationException>(() => db.GetBySingle<SimplePerson>(("Name", "Duplicate")));
      Assert.Throws<InvalidOperationException>(() => db.RunSingleQuery<SimplePerson>("SELECT * FROM People"));
    });
  }

  [TestCase(false)]
  [TestCase(true)]
  public void UpdateRejectsNullForRequiredColumnsWithoutChangingTheRow(bool useEditData)
  {
    CreateFactory().Action(db =>
    {
      var person = new SimplePerson { Name = "Required", Number = 42 };
      db.Add(person);

      if (useEditData)
      {
        var edits = EditData.Create<SimplePerson>();
        edits.AddProperty("Name", typeof(string), null);

        Assert.Throws<SqliteException>(() => db.UpdateEntity<SimplePerson>(person.ID, edits));
      }
      else
      {
        Assert.Throws<SqliteException>(() => db.UpdateEntity<SimplePerson>(person.ID, ("Name", null)));
      }

      Assert.That(db.GetById<SimplePerson>(person.ID)!.Name, Is.EqualTo("Required"));
    });
  }

  [Test]
  public void RawHelpersPropagateSqlErrorsAndHandlerRemainsUsable()
  {
    CreateFactory().Action(db =>
    {
      Assert.Throws<SqliteException>(() => db.RunQuery<SimplePerson>("SELECT * FROM MissingTable"));
      Assert.Throws<SqliteException>(() => db.RunSingleQuery<SimplePerson>("SELECT * FROM MissingTable"));
      Assert.Throws<SqliteException>(() => db.RunExecute("DELETE FROM MissingTable"));

      Assert.That(db.GetCount<SimplePerson>(), Is.Zero);
    });
  }

  [Test]
  public void AddRejectsNullAndPreviouslySavedEntitiesWithoutAddingRows()
  {
    CreateFactory().Action(db =>
    {
      Assert.Throws<ArgumentNullException>(() => db.Add<SimplePerson>(null!));
      Assert.Throws<InvalidOperationException>(() => db.Add(new SimplePerson { ID = 123, Name = "Existing" }));
      Assert.That(db.GetCount<SimplePerson>(), Is.Zero);
    });
  }

  [Test]
  public void CrudRejectsTypesOutsideTheSchema()
  {
    CreateFactory().Action(db =>
    {
      Assert.Throws<InvalidOperationException>(() => db.Add(new OTOTarget { Name = "Unknown" }));
      Assert.Throws<InvalidOperationException>(() => db.GetById<OTOTarget>(1));
      Assert.Throws<InvalidOperationException>(() => db.GetAll<OTOTarget>());
      Assert.Throws<InvalidOperationException>(() => db.GetCount<OTOTarget>());
      Assert.Throws<InvalidOperationException>(() => db.DeleteById<OTOTarget>(1));
      Assert.Throws<InvalidOperationException>(() => db.UpdateEntity<OTOTarget>(1, ("Name", "Unknown")));
    });
  }

  [Test]
  public void DeleteRejectsUnassignedId()
  {
    CreateFactory().Action(db =>
    {
      Assert.Throws<InvalidOperationException>(() => db.DeleteById<SimplePerson>(0));
    });
  }

  [TestCase(0, 20, "pageNumber")]
  [TestCase(-1, 20, "pageNumber")]
  [TestCase(1, 0, "pageSize")]
  [TestCase(1, -1, "pageSize")]
  public void PagingRejectsNonPositiveArguments(int page, int size, string parameter)
  {
    CreateFactory().Action(db =>
    {
      var error = Assert.Throws<ArgumentOutOfRangeException>(() => db.GetBy<SimplePerson>(new { Name = "Test" }, pageNumber: page, pageSize: size));

      Assert.That(error!.ParamName, Is.EqualTo(parameter));
    });
  }

  [Test]
  public void FiltersRejectNullEmptyAndDuplicateArguments()
  {
    CreateFactory().Action(db =>
    {
      Assert.Throws<ArgumentNullException>(() => db.GetBy<SimplePerson>((IList<(string, object)>)null!));
      Assert.Throws<ArgumentNullException>(() => db.GetBy<SimplePerson>(((string, object)[])null!));
      Assert.Throws<ArgumentNullException>(() => db.GetBySingle<SimplePerson>(null!));
      Assert.Throws<ArgumentException>(() => db.GetBy<SimplePerson>(Array.Empty<(string, object)>()));
      Assert.Throws<ArgumentException>(() => db.GetBySingle<SimplePerson>());
      Assert.Throws<ArgumentException>(() => db.GetBy<SimplePerson>(("Name", "One"), ("Name", "Two")));
      Assert.Throws<ArgumentException>(() => db.GetBySingle<SimplePerson>(("Name", "One"), ("Name", "Two")));
    });
  }

  [Test]
  public void ExampleQueriesRejectNullExamplesAndInvalidProjections()
  {
    CreateFactory().Action(db =>
    {
      Assert.Throws<ArgumentNullException>(() => db.GetBy<SimplePerson>((object)null!, orderBy: "ID"));
      Assert.Throws<ArgumentNullException>(() => db.GetBy<SimplePerson>(null!, new[] { "Name" }));
      Assert.Throws<ArgumentNullException>(() => db.GetBy<SimplePerson>(new { Name = "Test" }, (IList<string>)null!));
      Assert.Throws<ArgumentException>(() => db.GetBy<SimplePerson>(new { Name = "Test" }, Array.Empty<string>()));
    });
  }

  [TestCase(false)]
  [TestCase(true)]
  public void UpdateRejectsMissingRowsAndUnknownColumns(bool useEditData)
  {
    CreateFactory().Action(db =>
    {
      var person = new SimplePerson { Name = "Unchanged" };
      db.Add(person);

      if (useEditData)
      {
        var validEdits = EditData.Create<SimplePerson>();
        validEdits.AddProperty("Name", typeof(string), "After");

        var invalidEdits = EditData.Create<SimplePerson>();
        invalidEdits.AddProperty("MissingColumn", typeof(string), "After");

        Assert.Throws<InvalidOperationException>(() => db.UpdateEntity<SimplePerson>(999, validEdits));
        Assert.Throws<InvalidOperationException>(() => db.UpdateEntity<SimplePerson>(person.ID, invalidEdits));
      }
      else
      {
        Assert.Throws<InvalidOperationException>(() => db.UpdateEntity<SimplePerson>(999, ("Name", "After")));
        Assert.Throws<InvalidOperationException>(() => db.UpdateEntity<SimplePerson>(person.ID, ("MissingColumn", "After")));
      }

      Assert.That(db.GetById<SimplePerson>(person.ID)!.Name, Is.EqualTo("Unchanged"));
    });
  }

  [Test]
  public void UpdateRejectsNullEmptyPrimaryKeyOnlyAndDuplicateArguments()
  {
    CreateFactory().Action(db =>
    {
      var person = new SimplePerson { Name = "Unchanged" };
      db.Add(person);

      var keyOnly = EditData.Create<SimplePerson>();
      keyOnly.AddProperty("ID", typeof(int), person.ID);

      Assert.Throws<ArgumentNullException>(() => db.UpdateEntity<SimplePerson>(person.ID, (EditData)null!));
      Assert.Throws<ArgumentNullException>(() => db.UpdateEntity<SimplePerson>(person.ID, ((string, object?)[])null!));
      Assert.Throws<ArgumentException>(() => db.UpdateEntity<SimplePerson>(person.ID));
      Assert.Throws<ArgumentException>(() => db.UpdateEntity<SimplePerson>(person.ID, ("ID", 999)));
      Assert.Throws<ArgumentException>(() => db.UpdateEntity<SimplePerson>(person.ID, EditData.Create<SimplePerson>()));
      Assert.Throws<ArgumentException>(() => db.UpdateEntity<SimplePerson>(person.ID, keyOnly));
      Assert.Throws<ArgumentException>(() => db.UpdateEntity<SimplePerson>(person.ID, ("Name", "One"), ("Name", "Two")));
      Assert.That(db.GetById<SimplePerson>(person.ID)!.Name, Is.EqualTo("Unchanged"));
    });
  }

  [Test]
  public void ExpressionQueriesExplicitlyRemainUnsupported()
  {
    CreateFactory().Action(db =>
    {
      Assert.Throws<NotImplementedException>(() => db.Get<SimplePerson>(person => person.Number == 1));
    });
  }
}
