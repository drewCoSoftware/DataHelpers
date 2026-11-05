using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using DataHelpers;
using DataHelpers.Data;
using NUnit.Framework;

namespace DataHelpersTesters;

public class UnifiedFactoryTesters : TestBase
{
  [Test]
  public void ConcurrentActionsOwnSeparateHandlersAndDisposeThem()
  {
    var factory = CreateTestDataBaseFor<SimpleSchema>(nameof(ConcurrentActionsOwnSeparateHandlersAndDisposeThem));
    var handlers = new ConcurrentBag<DBHandler<SimpleSchema>>();

    Parallel.For(0, 8, _ =>
    {
      factory.Action(handler =>
      {
        handlers.Add(handler);

        handler.Action(inner =>
        {
          Assert.That(inner, Is.SameAs(handler));
          Assert.That(inner.GetCount<SimplePerson>(), Is.Zero);
        });
      });
    });

    Assert.That(handlers.Distinct().Count(), Is.EqualTo(8));

    foreach (var handler in handlers)
    {
      Assert.Throws<ObjectDisposedException>(() => handler.GetCount<SimplePerson>());
    }
  }

  [Test]
  public void FailedActionDisposesItsHandlerAndFactoryRemainsUsable()
  {
    var factory = CreateTestDataBaseFor<SimpleSchema>(nameof(FailedActionDisposesItsHandlerAndFactoryRemainsUsable));
    DBHandler<SimpleSchema>? captured = null;

    Assert.Throws<InvalidOperationException>(() => factory.Action(handler =>
    {
      captured = handler;

      throw new InvalidOperationException("Abort action");
    }));

    Assert.Throws<ObjectDisposedException>(() => captured!.GetCount<SimplePerson>());
    Assert.That(factory.GetCount<SimplePerson>(), Is.Zero);
  }

  [TestCase(false)]
  [TestCase(true)]
  public void AllConvenienceOperationsUseTheTransaction(bool rollback)
  {
    var factory = CreateTestDataBaseFor<SimpleSchema>($"{nameof(AllConvenienceOperationsUseTheTransaction)}_{rollback}");
    var original = new SimplePerson { Name = "Original" };
    factory.Add(original);

    void ExecuteTransaction()
    {
      factory.Transaction(db =>
      {
        var added = new SimplePerson { Name = "Added" };
        db.Add(added);

        Assert.That(db.GetAll<SimplePerson>().Length, Is.EqualTo(2));
        Assert.That(db.GetCount<SimplePerson>("Name = @name", new { name = "Added" }), Is.EqualTo(1));
        Assert.That(db.GetBy<SimplePerson>(("Name", "Added")).Single().ID, Is.EqualTo(added.ID));
        Assert.That(db.GetBy<SimplePerson>(new List<(string, object)> { ("Name", "Added") }).Single().ID, Is.EqualTo(added.ID));
        Assert.That(db.GetBySingle<SimplePerson>(("Name", "Added"))!.ID, Is.EqualTo(added.ID));
        Assert.That(db.GetBy<SimplePerson>(new { Name = "Added" }, new[] { "Name" }).Single().Name, Is.EqualTo("Added"));
        Assert.That(db.GetBy<SimplePerson>(new { Name = "Added" }, orderBy: "ID", pageSize: 1).Single().Name, Is.EqualTo("Added"));

        db.Action(inner => inner.UpdateEntity<SimplePerson>(original.ID, ("Name", "Tuple update")));

        var edits = EditData.Create<SimplePerson>();
        edits.AddProperty(nameof(SimplePerson.ID), typeof(int), added.ID);
        edits.AddProperty(nameof(SimplePerson.Name), typeof(string), "Edit update");
        db.UpdateEntity<SimplePerson>(original.ID, edits);

        Assert.That(db.GetById<SimplePerson>(original.ID)!.Name, Is.EqualTo("Edit update"));
        Assert.That(db.GetById<SimplePerson>(added.ID)!.Name, Is.EqualTo("Added"));
        Assert.That(db.Action(inner => inner.GetCount<SimplePerson>()), Is.EqualTo(2));

        db.DeleteById<SimplePerson>(added.ID);

        if (rollback)
        {
          throw new InvalidOperationException("Abort all operations");
        }
      });
    }

    if (rollback)
    {
      Assert.Throws<InvalidOperationException>(ExecuteTransaction);
    }
    else
    {
      ExecuteTransaction();
    }

    Assert.That(factory.GetAll<SimplePerson>().Single().Name, Is.EqualTo(rollback ? "Original" : "Edit update"));
  }

  [Test]
  public void ConvenienceMethodsAndRawQueriesShareTransaction()
  {
    var factory = CreateTestDataBaseFor<SimpleSchema>(nameof(ConvenienceMethodsAndRawQueriesShareTransaction));

    factory.Transaction(db =>
    {
      db.Add(new SimplePerson { Name = "Alice" });

      Assert.That(db.GetCount<SimplePerson>(), Is.EqualTo(1));
      Assert.That(db.RunSingleQuery<int>("SELECT COUNT(*) FROM People"), Is.EqualTo(1));

      db.UpdateEntity<SimplePerson>(1, ("Name", "Bob"));
    });

    Assert.That(factory.GetById<SimplePerson>(1)!.Name, Is.EqualTo("Bob"));
    Assert.That(factory.GetBy<SimplePerson>(new { Name = "Bob" }, new[] { "Name" }).Length, Is.EqualTo(1));
    Assert.That(factory.GetBy<SimplePerson>(new { Name = "Bob" }, orderBy: "ID", pageSize: 1).Length, Is.EqualTo(1));
    Assert.That(factory.RunQuery<SimplePerson>("SELECT * FROM People").Count(), Is.EqualTo(1));

    Assert.That(factory.DeleteById<SimplePerson>(1), Is.EqualTo(1));
  }

  [Test]
  public void FailedTransactionRollsBackConvenienceAndRawWrites()
  {
    var factory = CreateTestDataBaseFor<SimpleSchema>(nameof(FailedTransactionRollsBackConvenienceAndRawWrites));

    Assert.Throws<InvalidOperationException>(() => factory.Transaction(db =>
    {
      db.Add(new SimplePerson { Name = "Alice" });

      db.RunExecute("UPDATE People SET Name = @name", new { name = "Bob" });

      throw new InvalidOperationException("Abort");
    }));

    Assert.That(factory.GetCount<SimplePerson>(), Is.Zero);

    factory.Transaction(db =>
    {
      db.Add(new SimplePerson { Name = "Carol" });

      db.Rollback();
    });

    Assert.That(factory.GetCount<SimplePerson>(), Is.Zero);
  }
}
