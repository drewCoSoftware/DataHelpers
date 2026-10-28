# DataHelpers
Some code to help out with interacting with different databases.  
Currently there is some support for Postgres and SQLite databases.  

The goal of this project is to be able to take an object graph and create simple query syntax automatically.  Basically, it is a kind of ORM.  It is not intended to
solve all database/ORM problems, and is focused on easy setup and simple data storage / retrieval.

For a step-by-step guide to defining a schema, creating a database, and performing basic CRUD operations with `IDataFactory`, see the [examples guide](examples.md). All examples use SQLite and build on the same schema.


## Example:

This example uses the same `ExampleSchema` and `Person` entity as the examples guide. In a console application that references DataHelpers, the following code generates a parameterized insert query using the SQLite flavor.

```csharp
using System;
using System.Collections.Generic;
using DataHelpers.Data;

// Create a schema definition from the schema type.
var flavor = new SqliteFlavor();
Type schemaType = typeof(ExampleSchema);
var schema = new SchemaDefinition(flavor, schemaType);

// Look up the table by the schema's data-set property name.
string tableName = nameof(ExampleSchema.People);
TableDef tableDef = schema.GetTableDef(tableName)!;

string insertQuery = tableDef.GetInsertQuery();
Console.WriteLine(insertQuery);

// IHasPrimary identifies ID as the entity's integer primary key.
public class Person : IHasPrimary
{
    public int ID { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}

// Each list property defines a table of the corresponding entity type.
public class ExampleSchema
{
    public List<Person> People { get; set; } = new List<Person>();
}
```

`GetTableDef` throws if the named data set is missing unless `allowNull` is set to `true`. This example generates SQL; it does not create a database or insert a row. See the examples guide above to execute database operations.
