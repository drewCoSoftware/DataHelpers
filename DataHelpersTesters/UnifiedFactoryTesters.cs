using System;
using System.Linq;
using DataHelpers.Data;
using NUnit.Framework;

namespace DataHelpersTesters;

public class UnifiedFactoryTesters : TestBase
{
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
