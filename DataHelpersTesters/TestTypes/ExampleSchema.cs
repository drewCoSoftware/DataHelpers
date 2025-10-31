// ==========================================================================   
using System;
using System.Collections.Generic;
using DataHelpers;
using DataHelpers.Data;

namespace DataHelpersTesters;



// ==============================================================================================================================
/// <summary>
/// This shows that schema generation will auto-create a mapping table that links people
/// to places that they have visited.
/// </summary>
public class VacationSchema
{
  public List<Traveler> People { get; set; } = new List<Traveler>();
  public List<Place> Places { get; set; } = new List<Place>();

  // This is a mapping table for people-places.
  public List<PeopletoPlaces> PeopleToPlaces { get; set; }
}


// ==============================================================================================================================
// TODO: This needs to be updated to expicitly point to datasets on 'VacationSchema', not just a single data type!
[MappingTable(typeof(Traveler), typeof(Place), nameof(People_ID), nameof(Place_ID))]
public class PeopletoPlaces
{
  public int People_ID { get; set; }
  public int Place_ID { get; set; }
}

// ==============================================================================================================================
public class Traveler : IHasPrimary
{
  public int ID { get; set; }
  public string Name { get; set; }

  /// <summary>
  /// All of the places that this person has visited.
  /// </summary>
  [Relationship(DataSet = nameof(PeopletoPlaces), TargetProperty = nameof(PeopletoPlaces.Place_ID))]
  public List<Place> PlacesVisited { get; set; }
}

// ==============================================================================================================================
public class Place : IHasPrimary
{
  public int ID { get; set; }
  public string Name { get; set; }
  public string Country { get; set; }

  /// <summary>
  /// All of the people that have visited this place.
  /// </summary>
  [Relationship(DataSet = nameof(PeopletoPlaces), TargetProperty = nameof(PeopletoPlaces.People_ID))]
  public List<Traveler> Visitors { get; set; }
}


// ==============================================================================================================================
public class TestSchema2
{
  public List<TestPerson2> People { get; set; }
  public List<House> Domiciles { get; set; }
}

// ==============================================================================================================================
public class House : IHasPrimary
{
  public int ID { get; set; }
  public string Address { get; set; }
}

// ==============================================================================================================================
public class TestPerson2
{
  public int ID { get; set; }
  public string Name { get; set; }

  [Relationship(DataSet = nameof(TestSchema2.Domiciles))]
  public Relation<House> Home { get; set; }
}


// ==========================================================================
public class ExampleSchema
{
  // NOTE: In the schema, these lists represent tables of data.
  // Could a schema have a single entry table represented as well?
  // It could be useful to have a different generic type to represent a set<T> vs.
  // a list?
  // --> NOTE: We are using these schema types as a declaration of the overall data,
  // not as a means to resolve that data!
  public List<ExampleParent> Parents { get; set; } = new List<ExampleParent>();
  public List<ExampleChild> Kids { get; set; } = new List<ExampleChild>();
  public List<SomeData> SomeData { get; set; } = new List<SomeData>();
  public List<OneMember> Onesies { get; set; } = new List<OneMember>();
}

// ==========================================================================
public class BusinessSchema
{
  public List<Person> People { get; set; } = new List<Person>();
  public List<Address> Addresses { get; set; } = new List<Address>();
  public List<ClientAccount> ClientAccounts { get; set; } = new List<ClientAccount>();
  public List<Town> Towns { get; set; } = new List<Town>();
}

// ==========================================================================
public class Person : IHasPrimary
{
  public int ID { get; set; }
  public string Name { get; set; }
  public int Number { get; set; }

  /// <summary>
  /// Shows that we can use an explicit name for the data set that this is related to.
  /// </summary>
  [Relationship(DataSet = nameof(BusinessSchema.Addresses))]
  public Address Address { get; set; }

  //  public int Address_ID { get; set; }

  /// <summary>
  /// This will automatically use the data set name 'ClientAccounts'
  /// Note that this relationship, as far as a database is concerned, will require an FK on
  /// the associated data set, and this FK will be created automatically.
  /// </summary>
  [Relationship]
  public List<ClientAccount> ClientAccounts { get; set; } = null;
}

// ==========================================================================
public class Address : IHasPrimary
{
  public int ID { get; set; }
  public string Street { get; set; }
  public string City { get; set; }
  public string State { get; set; }
}

// ==========================================================================
public class ClientAccount : IHasPrimary
{
  public int ID { get; set; }
  public string ClientName { get; set; }

  // NOTE: This should resolve to the 'ClientAccounts' property on 'Person'
  // This is how we can model a 'bi-directional relationship.
  [Relationship(DataSet = nameof(BusinessSchema.People))]
  public Person AccountManager { get; set; }
}

// ==========================================================================
public class Town : IHasPrimary
{
  public int ID { get; set; }
  public string Name { get; set; }

  /// <summary>
  /// This shows that we can associate many addresses with a single 'town'
  /// but there doesn't need to be a bi-directional relationship.
  /// An FK to this will be created on the 'Address' table.
  /// </summary>
  [Relationship(DataSet = nameof(BusinessSchema.Addresses))]
  public List<Address> Addresses { get; set; }
}


// ==========================================================================
/// <summary>
/// Just a regular old table with no relations.
/// </summary> 
public class SomeData : IHasPrimary
{
  public int ID { get; set; }
  public string Name { get; set; }
  public int Number { get; set; }
  public DateTimeOffset Date { get; set; }
}

// ==========================================================================   
public class OneMember : IHasPrimary
{
  public int ID { get; set; }
  public string Name { get; set; }
}

// ==========================================================================   
public class ExampleParent : IHasPrimary
{
  public int ID { get; set; }
  public string Name { get; set; }
  public DateTimeOffset CreateDate { get; set; }

  // This parent has many different children.
  // That means that each child will have an FK that refers back to this specific parent.
  // The parent table does NOT point to its children directly.
  [Relationship(DataSet = nameof(ExampleSchema.Kids))]
  public List<ExampleChild> Children { get; set; } = new List<ExampleChild>();
}

// ==========================================================================
public class ExampleChild : IHasPrimary
{
  public int ID { get; set; }
  public string? Label { get; set; }

  [Relationship(DataSet = nameof(ExampleSchema.Parents))]
  public ExampleParent Parent { get; set; }
}
