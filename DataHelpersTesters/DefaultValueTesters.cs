using System;
using DataHelpers.Data;
using NUnit.Framework;

namespace DataHelpersTesters;

public class DefaultValueTesters : TestBase
{
  [Test]
  public void SqliteAppliesDefaultsToOmittedColumns()
  {
    var factory = CreateTestDataBaseFor<DefaultValueSchema>(nameof(SqliteAppliesDefaultsToOmittedColumns));
    var table = factory.Schema.GetTableDef<DefaultValueRow>();
    string createQuery = table.GetCreateQuery();

    Assert.That(createQuery, Does.Not.Contain("DEFAULT NULL"));

    string insert = $"INSERT INTO {table.Name} DEFAULT VALUES";
    int inserted = factory.RunExecute(insert);
    Assert.That(inserted, Is.EqualTo(1));

    var row = factory.GetById<DefaultValueRow>(41);
    Assert.That(row, Is.Not.Null);
    Assert.That(row!.Quantity, Is.EqualTo(123));
    Assert.That(row.OptionalQuantity, Is.Zero);
    Assert.That(row.Label, Is.EqualTo("O'Brien"));
    Assert.That(row.Active, Is.True);
    Assert.That(row.Status, Is.EqualTo(RowStatus.Active));
    Assert.That(row.Note, Is.Null);
  }

  [Test]
  public void AddAppliesDefaultsForUnsetPropertiesAndKeepsSuppliedValues()
  {
    var factory = CreateTestDataBaseFor<DefaultValueSchema>(nameof(AddAppliesDefaultsForUnsetPropertiesAndKeepsSuppliedValues));
    var row = new DefaultValueRow { Quantity = 5, Label = "Supplied", Active = false, Status = RowStatus.Inactive };

    int id = factory.Add(row);
    var saved = factory.GetById<DefaultValueRow>(id);

    Assert.That(id, Is.EqualTo(41));
    Assert.That(saved, Is.Not.Null);
    Assert.That(saved!.OptionalQuantity, Is.Zero);
    Assert.That(saved.Quantity, Is.EqualTo(5));
    Assert.That(saved.Label, Is.EqualTo("Supplied"));
    Assert.That(saved.Active, Is.False);
    Assert.That(saved.Status, Is.EqualTo(RowStatus.Inactive));
  }

  [Test]
  public void SqliteGeneratesColumnDefaults()
  {
    var factory = CreateTestDataBaseFor<DefaultValueSchema>(nameof(SqliteGeneratesColumnDefaults));
    var table = factory.Schema.GetTableDef<DefaultValueRow>();
    string createQuery = table.GetCreateQuery();

    Assert.That(createQuery, Does.Contain("id INT PRIMARY KEY NOT NULL DEFAULT 41"));
    Assert.That(createQuery, Does.Contain("quantity INTEGER NOT NULL DEFAULT 123"));
    Assert.That(createQuery, Does.Contain("optionalquantity INTEGER NULL DEFAULT 0"));
    Assert.That(createQuery, Does.Contain("label TEXT NOT NULL DEFAULT 'O''Brien'"));
    Assert.That(createQuery, Does.Contain("active INTEGER NOT NULL DEFAULT 1"));
    Assert.That(createQuery, Does.Contain("status INTEGER NOT NULL DEFAULT 8"));
    Assert.That(createQuery, Does.Not.Contain("DEFAULT NULL"));
  }

  [Test]
  public void PostgresGeneratesColumnDefaults()
  {
    var flavor = new PostgresFlavor();
    var schema = new SchemaDefinition(flavor, typeof(DefaultValueSchema));
    var table = schema.GetTableDef<DefaultValueRow>();
    string createQuery = table.GetCreateQuery();

    Assert.That(createQuery, Does.Contain("id integer PRIMARY KEY NOT NULL DEFAULT 41"));
    Assert.That(createQuery, Does.Contain("quantity integer NOT NULL DEFAULT 123"));
    Assert.That(createQuery, Does.Contain("optionalquantity integer NULL DEFAULT 0"));
    Assert.That(createQuery, Does.Contain("label text NOT NULL DEFAULT 'O''Brien'"));
    Assert.That(createQuery, Does.Contain("active boolean NOT NULL DEFAULT true"));
    Assert.That(createQuery, Does.Contain("status integer NOT NULL DEFAULT 8").IgnoreCase);
    Assert.That(createQuery, Does.Not.Contain("DEFAULT NULL"));
  }

  [Test]
  public void SchemaRejectsDefaultWithMismatchedType()
  {
    var error = Assert.Throws<InvalidOperationException>(BuildMismatchedSchema);
    Assert.That(error!.Message, Does.Contain($"{nameof(MismatchedDefaultRow)}.{nameof(MismatchedDefaultRow.Quantity)}"));
  }

  private static void BuildMismatchedSchema()
  {
    CreateTestDataBaseFor<MismatchedDefaultSchema>(nameof(SchemaRejectsDefaultWithMismatchedType));
  }

  public enum RowStatus
  {
    Inactive = 0,
    Active = 8
  }

  public class DefaultValueRow : IHasPrimary
  {
    private int _ID;
    private int _Quantity;
    private int? _OptionalQuantity;
    private string _Label = "";
    private bool _Active;
    private RowStatus _Status;
    private string? _Note;

    [DefaultValue(Value = 41)]
    public int ID { get { return _ID; } set { _ID = value; } }

    [DefaultValue(Value = 123)]
    public int Quantity { get { return _Quantity; } set { _Quantity = value; } }

    [DefaultValue(Value = 0)]
    public int? OptionalQuantity { get { return _OptionalQuantity; } set { _OptionalQuantity = value; } }

    [DefaultValue(Value = "O'Brien")]
    public string Label { get { return _Label; } set { _Label = value; } }

    [DefaultValue(Value = true)]
    public bool Active { get { return _Active; } set { _Active = value; } }

    [DefaultValue(Value = RowStatus.Active)]
    public RowStatus Status { get { return _Status; } set { _Status = value; } }

    [DefaultValue(Value = null)]
    public string? Note { get { return _Note; } set { _Note = value; } }
  }

  public class DefaultValueSchema
  {
    private DefaultValueRow _Rows = null!;
    public DefaultValueRow Rows { get { return _Rows; } set { _Rows = value; } }
  }

  public class MismatchedDefaultRow
  {
    private long _Quantity;

    [DefaultValue(Value = 123)]
    public long Quantity { get { return _Quantity; } set { _Quantity = value; } }
  }

  public class MismatchedDefaultSchema
  {
    private MismatchedDefaultRow _Rows = null!;
    public MismatchedDefaultRow Rows { get { return _Rows; } set { _Rows = value; } }
  }
}
