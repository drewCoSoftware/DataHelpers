using System.Collections.Generic;
using DataHelpers;
using DataHelpers.Data;

namespace DataHelpersTesters;

public class UniqueAssociationSchema
{
  public List<UniqueAssociationOwner> Owners { get; set; } = new List<UniqueAssociationOwner>();
  public List<UniqueAssociationTarget> Targets { get; set; } = new List<UniqueAssociationTarget>();
  public List<UniqueAssociationChild> Children { get; set; } = new List<UniqueAssociationChild>();
}

public class UniqueAssociationOwner : IHasPrimary
{
  public int ID { get; set; }

  [Unique]
  [Association(nameof(UniqueAssociationSchema.Targets), AssociationName = "ExclusiveTarget")]
  public SingleAssociation<UniqueAssociationTarget> Target { get; set; } = new SingleAssociation<UniqueAssociationTarget>();

  [Unique(Group = "TargetNumber")]
  [Association(nameof(UniqueAssociationSchema.Targets), AssociationName = "GroupedTarget")]
  public SingleAssociation<UniqueAssociationTarget> GroupedTarget { get; set; } = new SingleAssociation<UniqueAssociationTarget>();

  [Unique(Group = "TargetNumber")]
  public int Number { get; set; }

  [Unique]
  [Association(nameof(UniqueAssociationSchema.Children), AssociationName = "ExclusiveOwner")]
  public ManyAssociation<UniqueAssociationChild> Children { get; set; } = new ManyAssociation<UniqueAssociationChild>();

  [Unique(Group = "OwnerNumber")]
  [Association(nameof(UniqueAssociationSchema.Children), AssociationName = "GroupedOwner")]
  public ManyAssociation<UniqueAssociationChild> GroupedChildren { get; set; } = new ManyAssociation<UniqueAssociationChild>();
}

public class UniqueAssociationTarget : IHasPrimary
{
  public int ID { get; set; }
  public int Number { get; set; }
}

public class UniqueAssociationChild : IHasPrimary
{
  public int ID { get; set; }

  [Association(nameof(UniqueAssociationSchema.Owners), AssociationName = "ExclusiveOwner")]
  public SingleAssociation<UniqueAssociationOwner> Owner { get; set; } = new SingleAssociation<UniqueAssociationOwner>();

  [Association(nameof(UniqueAssociationSchema.Owners), AssociationName = "GroupedOwner")]
  public SingleAssociation<UniqueAssociationOwner> GroupedOwner { get; set; } = new SingleAssociation<UniqueAssociationOwner>();

  [Unique(Group = "OwnerNumber")]
  public int Number { get; set; }
}
