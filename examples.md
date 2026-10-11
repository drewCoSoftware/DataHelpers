# DataHelpers examples

## Table of contents

<!-- Keep this table of contents in sync whenever topics or examples are added, renamed, moved, or removed. -->

- [Define a schema and create a SQLite database](#define-a-schema-and-create-a-sqlite-database)
  - [Define the entity and schema](#1-define-the-entity-and-schema)
  - [Create the database from the schema](#2-create-the-database-from-the-schema)
  - [Inspect the generated schema](#3-inspect-the-generated-schema-optional)
  - [Existing databases](#existing-databases)
- [Basic CRUD through IDataFactory](#basic-crud-through-idatafactory)
  - [Create: Add](#create-add)
  - [Read: GetById, GetBy, and GetAll](#read-getbyid-getby-and-getall)
  - [Update: UpdateEntity](#update-updateentity)
  - [Delete: DeleteById](#delete-deletebyid)

DataHelpers supports multiple database flavors, including SQLite (`SqliteFlavor`) and PostgreSQL (`PostgresFlavor`). A flavor supplies the database-specific SQL behavior.

All examples in this document use the SQLite flavor. SQLite keeps setup simple by storing the database in a local file without requiring a database server.

All examples share the same `ExampleSchema` and `Person` entity defined below. Later examples build on this database setup and reuse these types.

## Define a schema and create a SQLite database

This example uses a .NET 8 console application that references the `DataHelpers` project.

### 1. Define the entity and schema

An entity class describes a row in a table. Implement `IHasPrimary` and provide an `int ID` property for its primary key. The other scalar properties describe the columns to store.

A schema class groups the entities into public `List<T>` properties. Each property defines a data set (table); its name supplies the table name. For example, `People` defines a table of `Person` rows.

Create `ExampleSchema.cs`:

```csharp
using System.Collections.Generic;
using DataHelpers.Data;

public class Person : IHasPrimary
{
    public int ID { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}

public class ExampleSchema
{
    public List<Person> People { get; set; } = new List<Person>();
}
```

The library reads the schema's type to discover its tables and columns. You do not need to instantiate `ExampleSchema` or populate its list to create the database.

### 2. Create the database from the schema

Put the following in `Program.cs`:

```csharp
using System;
using System.IO;
using DataHelpers.Data;

string dataDirectory = Path.Combine(AppContext.BaseDirectory, "data");

var factory = new SqliteDataFactory<ExampleSchema>(
    dataDirectory,
    "example.sqlite");

factory.SetupDatabase();

Console.WriteLine($"Database ready: {factory.DBFilePath}");
```

The factory builds a `SchemaDefinition` from `ExampleSchema` using `SqliteFlavor`. Calling `SetupDatabase()` creates the data directory, database file, and tables when starting with a new database. This example creates `data/example.sqlite` under the application's output directory, containing the `People` table. If the supplied filename has no `.sqlite` suffix, the factory adds it.

Constructing the factory alone does not create the database; call `SetupDatabase()` before accessing its data.

### 3. Inspect the generated schema (optional)

The factory exposes the schema it used. Add this after creating the factory to inspect the SQL:

```csharp
Console.WriteLine(factory.Schema.GetCreateSQL());
```

You can also generate the schema SQL without creating a database:

```csharp
using DataHelpers;
using DataHelpers.Data;

var schema = new SchemaDefinition(new SqliteFlavor(), typeof(ExampleSchema));
string createSql = schema.GetCreateSQL();
```

### Existing databases

`SetupDatabase()` checks whether the database file and expected tables exist. When they do, it leaves the database in place. Its current validation does not compare existing columns or migrate schema changes, so changes to entity properties require separate schema updates.

## Basic CRUD through IDataFactory

`IDataFactory<ExampleSchema>` provides create, read, update, and delete operations. The examples below work through that interface, regardless of the concrete factory instance supplied. They reuse the `ExampleSchema` and `Person` types from the database setup above.

Add `using DataHelpers;` at the top of `Program.cs`. After the setup code, assign the existing factory to an interface variable:

```csharp
IDataFactory<ExampleSchema> data = factory;
```

Use the interface type when calling these methods: they are default interface implementations and are not directly exposed on a variable inferred as `SqliteDataFactory<ExampleSchema>` with `var`. Application methods can also accept an `IDataFactory<ExampleSchema>` parameter to use a factory supplied by their caller.

Run the following snippets in order, in the same scope after setup.

### Create: Add

Create a new entity with its `ID` left at zero. `Add` inserts it, returns its generated ID, and assigns that ID to the entity.

```csharp
var person = new Person
{
    Name = "Alex Smith",
    Age = 30
};

int personId = data.Add(person);
Console.WriteLine($"Created person {personId}; entity ID is {person.ID}.");
```

Calling `Add` with a nonzero ID throws an exception. Use `UpdateEntity` to change an existing row.

### Read: GetById, GetBy, and GetAll

Use `GetById` to retrieve one entity by its primary key. It returns `null` when no row matches.

```csharp
Person? savedPerson = data.GetById<Person>(personId);
if (savedPerson != null)
{
    Console.WriteLine($"{savedPerson.Name} is {savedPerson.Age} years old.");
}
```

Use `GetBy` to look up one entity by named properties. All supplied properties must match. The ID below keeps the lookup unique even if other people share the same name.

```csharp
Person? matchingPerson = data.GetBy<Person>(
    (nameof(Person.ID), personId),
    (nameof(Person.Name), "Alex Smith"));

Console.WriteLine(matchingPerson?.Name ?? "No matching person.");
```

`GetBy` returns `null` when no row matches and throws if more than one row matches. Use criteria that identify a single row.

Use `GetAll` to load every person into an array:

```csharp
Person[] people = data.GetAll<Person>();
foreach (Person item in people)
{
    Console.WriteLine($"{item.ID}: {item.Name}, age {item.Age}");
}
```

`GetAll` loads the entire table into memory, so it is best suited to small data sets. The expression-based `Get<TEntity>(predicate)` method is currently unfinished; use the implemented read methods shown here.

### Update: UpdateEntity

Pass the existing entity's ID and the properties to change. Use `nameof` to refer to property names.

```csharp
data.UpdateEntity<Person>(personId,
    (nameof(Person.Name), "Alex Jones"),
    (nameof(Person.Age), 31));

Person? updatedPerson = data.GetById<Person>(personId);
if (updatedPerson != null)
{
    Console.WriteLine($"Updated: {updatedPerson.Name}, age {updatedPerson.Age}");
}
```

This updates the stored row; previously loaded objects are not refreshed automatically. `UpdateEntity` returns no value and throws if it does not update exactly one row, including when the ID does not exist.

### Delete: DeleteById

Pass the primary key to delete the row. The return value is the number of rows deleted.

```csharp
int deletedCount = data.DeleteById<Person>(personId);
Console.WriteLine($"Deleted {deletedCount} person(s).");

Person? deletedPerson = data.GetById<Person>(personId);
Console.WriteLine(deletedPerson == null ? "Person no longer exists." : "Person still exists.");
```

Deleting an ID that does not exist returns zero. Passing an ID of zero throws an exception.
