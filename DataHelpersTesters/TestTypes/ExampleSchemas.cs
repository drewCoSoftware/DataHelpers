// ==========================================================================   
using System.Collections.Generic;
using DataHelpers;
using DataHelpers.Data;

namespace DataHelpersTesters;


// ==============================================================================================================================
/// <summary>
/// This schema only contains sets that don't have any relations to one another.
/// </summary>
public class SimpleSchema
{
  public List<SimplePerson> People { get; set; } = new List<SimplePerson>();
}

// ==============================================================================================================================
public class SimplePerson : IHasPrimary
{
  public int ID { get; set; }
  public string Name { get; set; }
  public int Number { get; set; }
}


// ==============================================================================================================================
/// <summary>
/// This shows that schema generation will auto-create a mapping table that links people
/// to places that they have visited.
/// </summary>
public class VacationSchema
{
  public List<Traveler> Travelers { get; set; } = new List<Traveler>();
  public List<Place> Places { get; set; } = new List<Place>();

  // NOTE: Mapping tables are auto-generated and don't need to be first-class entities (maybe they could be tho?)
}

// ==============================================================================================================================
public class Traveler : IHasPrimary
{
  public int ID { get; set; }
  public string Name { get; set; }

  /// <summary>
  /// All of the places that this person has visited.
  /// </summary>
  [Relation(DataSetName = nameof(VacationSchema.Places))]
  public ManyRelation<Place> PlacesVisited { get; set; }
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
  [Relation(DataSetName = nameof(VacationSchema.Travelers))]
  public ManyRelation<Person> Visitors { get; set; }
}

// ==========================================================================
public class BusinessSchema
{
  public List<Person> People { get; set; } = new List<Person>();
  public List<Address> Addresses { get; set; } = new List<Address>();
  public List<Town> Towns { get; set; } = new List<Town>();
  //public List<ClientAccount> ClientAccounts { get; set; } = new List<ClientAccount>();
}

// ==========================================================================
public class Person : IHasPrimary
{
  public int ID { get; set; }
  public string Name { get; set; }
  public int Number { get; set; }

  /// <summary>
  /// This data comes from the 'People' dataset of the schema.
  /// </summary>
  [Relation(nameof(BusinessSchema.Addresses))]
  public SingleRelation<Address> Address { get; set; }

  ///// <summary>
  ///// Shows that we can use an explicit name for the data set that this is related to.
  ///// </summary>
  //[Relationship(DataSet = nameof(BusinessSchema.Addresses))]
  //public Address Address { get; set; }

  ////  public int Address_ID { get; set; }

  ///// <summary>
  ///// This will automatically use the data set name 'ClientAccounts'
  ///// Note that this relationship, as far as a database is concerned, will require an FK on
  ///// the associated data set, and this FK will be created automatically.
  ///// </summary>
  //[Relationship]
  //public List<ClientAccount> ClientAccounts { get; set; } = null;
}

// ==========================================================================
public class Address : IHasPrimary
{
  public int ID { get; set; }
  public string Street { get; set; }
  public string City { get; set; }
  public string State { get; set; }

  // NOTE: Even tho 'Town' is related to many addresses, we don't represent
  // that relationship on purpose.  The point is to show that our generated
  // schemas can add members to the defs as needed to support the model.
  // NOTE: I think that if this column isn't explicitly defined it can be nullable...
  // of course, if it isn't defined, then there is really no good way to set the data,
  // so we should make it required instead?
  // NOTE: What can we do about setting this as a 'SingleRelation'?
  // Should that be required?
  // --> Yes!
  // public int Towns_ID { get; set; }
  [Relation(DataSetName = nameof(BusinessSchema.Towns))]
  public SingleRelation<Town> Town { get; set; } // = new SingleRelation<Town>();
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
  [Relation(DataSetName = nameof(BusinessSchema.Addresses))]
  public ManyRelation<Address> Addresses { get; set; } // = new ManyRelation<Address>();
}

// ==========================================================================
public class ClientAccount : IHasPrimary
{
  public int ID { get; set; }
  public string ClientName { get; set; }

  // NOTE: This should resolve to the 'ClientAccounts' property on 'Person'
  // This is how we can model a 'bi-directional relationship.
  [RelationAttribute(DataSetName = nameof(BusinessSchema.People))]
  public Person AccountManager { get; set; }
}
