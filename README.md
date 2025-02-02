# DataHelpers
Some code to help out with interacting with different databases.  
Currently there is some support for Postgres and SQLite databases.  

The goal of this project is to be able to take an object graph and create simple query syntax automatically.  Basically, it is a kind of ORM.  It is not intended to
solve all database/ORM problems, and is focused on easy setup and simple data storage / retrieval.


## Example:

```
// Define a simple schema:

// A normal data type.
// The IHasPrimary interface is used to indicate that ID is a unique identifer for your type.
public class SomeData : IHasPrimary
{
  public int ID { get; set; }
  public string Name { get; set; }
  public int Number { get; set; }
  public DateTimeOffset Date { get; set; }
}

// Each schema contains lists of your data types.
class ExampleSchema
{
    public List<SomeData> SomeData { get; set; } = new List<SomeData>();
}


// A Schema definition can be created:
var schema = new SchemaDefinition(new SqliteFlavor(), typeof(ExampleSchema));

// From the schema definiton, you can find the table definitions and use those
// to generate your queries:
TableDef tableDef = schema.GetTableDef(nameof(SomeData));

string insertQuery = tableDef.GetInsertQuery();

```
