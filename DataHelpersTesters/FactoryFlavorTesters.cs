using System;
using System.IO;
using DataHelpers;
using DataHelpers.Data;
using DataHelpers.SqlFlavors.Postgres;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace DataHelpersTesters;

public class FactoryFlavorTesters : TestBase
{
  private class ConfiguredSqliteFlavor : SqliteFlavor
  {
    public ConfiguredSqliteFlavor(string configuration)
    {
      Configuration = configuration;
    }

    public string Configuration { get; }
  }

  [TestCase(false)]
  [TestCase(true)]
  public void ProviderChoiceUsesTheSameInterfaceAndPreservesSuppliedFlavor(bool postgres)
  {
    ISqlFlavor flavor = postgres ? new PostgresFlavor() : new ConfiguredSqliteFlavor("Development");
    IDataFactory<SimpleSchema> factory;

    if (postgres)
    {
      factory = new PostgresDataFactory<SimpleSchema>("Host=localhost;Database=test", flavor);
    }
    else
    {
      factory = new SqliteDataFactory<SimpleSchema>("./test-data", "Flavor", flavor);
    }

    Assert.That(factory.Schema.Flavor, Is.SameAs(flavor));

    factory.Action(handler =>
    {
      Assert.That(handler.Schema, Is.SameAs(factory.Schema));
      Assert.That(handler.Schema.Flavor, Is.SameAs(flavor));
    });
  }

  [Test]
  public void PostgresConnectionPartsConstructorPreservesSuppliedFlavor()
  {
    var flavor = new PostgresFlavor();
    IDataFactory<SimpleSchema> factory = new PostgresDataFactory<SimpleSchema>("localhost", "test", "user", "password", 5432, flavor);

    Assert.That(factory.Schema.Flavor, Is.SameAs(flavor));
  }

  [Test]
  public void InjectedSqliteFlavorSupportsCrudAndTransactions()
  {
    var flavor = new ConfiguredSqliteFlavor("Development");
    string directory = Path.Combine(Path.GetTempPath(), "DataHelpers-tests");
    Directory.CreateDirectory(directory);

    var sqliteFactory = new SqliteDataFactory<SimpleSchema>(directory, Guid.NewGuid().ToString("N"), flavor);
    IDataFactory<SimpleSchema> factory = sqliteFactory;

    try
    {
      factory.SetupDatabase();

      factory.Transaction(handler =>
      {
        Assert.That(handler.Schema.Flavor, Is.SameAs(flavor));

        handler.Add(new SimplePerson { Name = "Injected flavor", Number = 42 });
      });

      Assert.That(factory.GetAll<SimplePerson>()[0].Number, Is.EqualTo(42));
    }
    finally
    {
      using (var connection = new SqliteConnection(sqliteFactory.ConnectionString))
      {
        SqliteConnection.ClearPool(connection);
      }

      File.Delete(sqliteFactory.DBFilePath);
    }
  }

  [Test]
  public void ConstructorsRejectNullFlavor()
  {
    Assert.Throws<ArgumentNullException>(() => new SqliteDataFactory<SimpleSchema>("./test-data", "Flavor", null!));
    Assert.Throws<ArgumentNullException>(() => new PostgresDataFactory<SimpleSchema>("Host=localhost", null!));
    Assert.Throws<ArgumentNullException>(() => new PostgresDataFactory<SimpleSchema>("localhost", "test", "user", "password", 5432, null!));
  }
}
