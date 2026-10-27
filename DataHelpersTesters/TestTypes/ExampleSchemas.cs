// ==========================================================================   
using System;
using System.Collections.Generic;
using System.Text.Json;
using DataHelpers;
using DataHelpers.Data;

namespace DataHelpersTesters;

// ==============================================================================================================================
// ==============================================================================================================================
public class MutualOneToOneSchema
{
  public List<MOTOSource> Sources { get; set; } = new List<MOTOSource>();
  public List<MOTOTarget> Targets { get; set; } = new List<MOTOTarget>();
}

// ==============================================================================================================================
public class MOTOSource : IHasPrimary
{
  public int ID { get; set; }
  public string Name { get; set; }

  [Association(nameof(OneWayOneToOneSchema.Targets))]
  public SingleAssociation<MOTOTarget> TargetSet { get; set; } = new SingleAssociation<MOTOTarget>();
}

// ==============================================================================================================================
public class MOTOTarget : IHasPrimary
{
  public int ID { get; set; }
  public string Name { get; set; }

  [Association(nameof(OneWayOneToOneSchema.Sources))]
  public SingleAssociation<MOTOSource> SourceSet { get; set; } = new SingleAssociation<MOTOSource>();
}

// ==============================================================================================================================
// ==============================================================================================================================
public class OneWayOneToOneSchema {
  public List<OTOSource> Sources { get; set; } = new List<OTOSource>();
  public List<OTOTarget> Targets { get; set; } = new List<OTOTarget>();
}

// ==============================================================================================================================
public class OTOSource : IHasPrimary
{
  public int ID { get; set; }
  public string Name { get; set; }

  [Association(nameof(OneWayOneToOneSchema.Targets))]
  public SingleAssociation<OTOTarget> Targets { get; set; } =new SingleAssociation<OTOTarget>();
}

// ==============================================================================================================================
public class OTOTarget : IHasPrimary
{
  public int ID { get; set; }
  public string Name { get; set; }
}

// ==============================================================================================================================
// ==============================================================================================================================
public class MutualManyToManySchema
{
  public List<ManyManySource> Sources { get; set; } = new List<ManyManySource>();
  public List<ManyManyTarget> Targets { get; set; } = new List<ManyManyTarget>();
}

// ==============================================================================================================================
public class ManyManySource : IHasPrimary
{
  public int ID { get; set; }
  public string Name { get; set; }

  /// <summary>
  /// Here we can see that we have a many relation to another set, but that set doesn't have
  /// any defined associations to this one.
  /// </summary>
  [Association(DataSetName = nameof(MutualManyToManySchema.Targets))]
  public ManyAssociation<ManyManyTarget> MMTargets { get; set; } = new ManyAssociation<ManyManyTarget>();
}

// ==============================================================================================================================
public class ManyManyTarget : IHasPrimary
{
  public int Number { get; set; }
  public int ID { get; set; }

  [Association(DataSetName = nameof(MutualManyToManySchema.Sources))]
  public ManyAssociation<ManyManySource> MMSources { get; set; } = new ManyAssociation<ManyManySource>();
}
// ==============================================================================================================================
// ==============================================================================================================================

// ==============================================================================================================================
// ==============================================================================================================================
public class SchemaWithRedundantAssociationDefs
{
  public List<MMOTO_MANY> ManyTable { get; set; } = new List<MMOTO_MANY>();
  public List<MMOTO_ONE> OneTable { get; set; } = new List<MMOTO_ONE>();
}

// ==============================================================================================================================
public class MMOTO_MANY : IHasPrimary
{
  public int ID { get; set; }
  public string Name { get; set; }

  /// <summary>
  /// Here we can see that we have a many relation to another set, but that set doesn't have
  /// any defined associations to this one.
  /// </summary>
  [Association(DataSetName = nameof(SchemaWithRedundantAssociationDefs.OneTable))]
  public ManyAssociation<MMOTO_ONE> OtherStuff { get; set; } = new ManyAssociation<MMOTO_ONE>();
}

// ==============================================================================================================================
public class MMOTO_ONE : IHasPrimary
{
  public int Number { get; set; }
  public int ID { get; set; }

  // This association is redundant, and should CRASH on schema generation.
  // Because an ID field that points back to the MMOTO_MANY set is created, this one is no longer needed!
  [Association(DataSetName = nameof(SchemaWithRedundantAssociationDefs.ManyTable))]
  public SingleAssociation<MMOTO_MANY> OtherStuff { get; set; } = new SingleAssociation<MMOTO_MANY>();

}
// ==============================================================================================================================
// ==============================================================================================================================


// ==============================================================================================================================
// ==============================================================================================================================
public class OneWayManyToOneSchema
{
  public List<HasMany> SomeTable { get; set; } = new List<HasMany>();
  public List<HasOne> SomeOtherTable { get; set; } = new List<HasOne>();
}

// ==============================================================================================================================
public class HasMany : IHasPrimary
{
  public int ID { get; set; }
  public string Name { get; set; }

  /// <summary>
  /// Here we can see that we have a many relation to another set, but that set doesn't have
  /// any defined associations to this one.
  /// </summary>
  [Association(DataSetName = nameof(OneWayManyToOneSchema.SomeOtherTable))]
  public ManyAssociation<HasOne> OtherStuff { get; set; } = new ManyAssociation<HasOne>();
}

// ==============================================================================================================================
public class HasOne : IHasPrimary
{
  public int Number { get; set; }
  public int ID { get; set; }
}
// ==============================================================================================================================
// ==============================================================================================================================


// ==============================================================================================================================
public class Matchup : IHasPrimary
{
  public int ID { get; set; }

  [Association(nameof(SchemaWithMappedTypes.Teams))]
  public SingleAssociation<Team> Favorite { get; set; } = new SingleAssociation<Team>();

  [Association(nameof(SchemaWithMappedTypes.Teams))]
  public SingleAssociation<Team>? Other { get; set; } = null;
}

// ==============================================================================================================================
public class Player : IHasPrimary
{
  public int ID { get; set; }
  public string Name { get; set; }
  public string Position { get; set; }

  [Association(nameof(SchemaWithMappedTypes.Teams))]
  public ManyAssociation<Team> Teams { get; set; }
}

// ==============================================================================================================================
public class Team : IHasPrimary
{
  public int ID { get; set; }
  public string Name { get; set; }

  [Association(nameof(SchemaWithMappedTypes.Players))]
  public ManyAssociation<Player> Players { get; set; } = new ManyAssociation<Player>();
}

// ==============================================================================================================================
public class SchemaWithMappedTypes
{
  public List<Player> Players { get; set; }
  public List<Team> Teams { get; set; }
  public List<Matchup> Matches { get; set; }
}



// ==============================================================================================================================
public enum ESomeEnum
{
  Val_0 = 0,
  Val_1 = 1,
  Val_2 = 2,
}

// ==============================================================================================================================
public class DataTypesSchema
{
  public List<XtraDataTypes> TheEntities { get; set; } = new List<XtraDataTypes>();
}

// ==============================================================================================================================
public class XtraDataTypes : IHasPrimary
{
  public int ID { get; set; }

  public Guid UserTag { get; set; }
  public DateTimeOffset CreatedOn { get; set; }
}

// ==============================================================================================================================
public class MultiColUnique : IHasPrimary
{
  public int ID { get; set; }

  [Unique(Group = "NameNumber")]
  public string Name { get; set; }

  [Unique(Group = "NameNumber")]
  public int Number { get; set; }
}

// ==============================================================================================================================
/// <summary>
/// This type has an invalid group specification for a multi-column unique index.
/// It will cause exceptions when we attempt to use it.
/// </summary>
public class InvalidMutiColUnique : IHasPrimary
{
  public int ID { get; set; }

  [Unique(Group = "NameNumber")]
  public string Name { get; set; }
}

// ==============================================================================================================================
public class CompositeType : ICompositeSerializer
{
  public string Name { get; set; }
  public int Number { get; set; }

  // --------------------------------------------------------------------------------------------------------------------------
  public object Deserialize(string data)
  {
    object res = JsonSerializer.Deserialize<CompositeType>(data);
    return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public string Serialize()
  {
    string res = JsonSerializer.Serialize<CompositeType>(this);
    return res;
  }
}

// ==============================================================================================================================
public class TypeWithComposite : IHasPrimary
{
  public int ID { get; set; }

  public CompositeType CompositeData { get; set; }
}

// ==============================================================================================================================
public class TypeWithEnum : IHasPrimary
{
  public int ID { get; set; }
  public ESomeEnum SomeEnum { get; set; }
}

// ==============================================================================================================================
public class AdvancedFeaturesSchema
{
  public List<TypeWithComposite> TypesWithComposites { get; set; }
  public List<TypeWithEnum> TypeWithEnum { get; set; }
  public List<MultiColUnique> TypeWithMulticolumnUnique { get; set; }
}

// ==============================================================================================================================
/// <summary>
/// This schema only contains sets that don't have any associations to one another.
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
  [Association(DataSetName = nameof(VacationSchema.Places))]
  public ManyAssociation<Place> PlacesVisited { get; set; }

  [Association(DataSetName = nameof(VacationSchema.Places), IDFieldName = "FavoritePlace_ID")]
  public SingleAssociation<Place>? FavoritePlace { get; set; } = null;
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
  [Association(DataSetName = nameof(VacationSchema.Travelers))]
  public ManyAssociation<Traveler> Visitors { get; set; }
}

// ==========================================================================
public class BusinessSchema
{
  public List<Person> People { get; set; } = new List<Person>();
  public List<Address> Addresses { get; set; } = new List<Address>();
  public List<Town> Towns { get; set; } = new List<Town>();
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
  [Association(nameof(BusinessSchema.Addresses))]
  public SingleAssociation<Address> Address { get; set; } = new SingleAssociation<Address>();

  [Association(nameof(BusinessSchema.Towns))]
  public SingleAssociation<Town>? HomeTown { get; set; } = null;
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
  // NOTE: What can we do about setting this as a 'SingleAssociation'?
  // Should that be required?
  // --> Yes!
  // public int Towns_ID { get; set; }
  [Association(DataSetName = nameof(BusinessSchema.Towns))]
  public SingleAssociation<Town> Town { get; set; }
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
  [Association(DataSetName = nameof(BusinessSchema.Addresses))]
  public ManyAssociation<Address> Addresses { get; set; } // = new ManyAssociation<Address>();
}

//// ==========================================================================
//public class ClientAccount : IHasPrimary
//{
//  public int ID { get; set; }
//  public string ClientName { get; set; }

//  // NOTE: This should resolve to the 'ClientAccounts' property on 'Person'
//  // This is how we can model a 'bi-directional relationship.
//  [RelationAttribute(DataSetName = nameof(BusinessSchema.People))]
//  public Person AccountManager { get; set; }
//}
