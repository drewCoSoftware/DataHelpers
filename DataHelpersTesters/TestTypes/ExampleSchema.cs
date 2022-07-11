// ==========================================================================   
using System;
using System.Collections.Generic;
using DataHelpers.Data;

namespace DataHelpersTesters;

class ExampleSchema
{
  // NOTE: In the schema, these lists represent tables of data.
  // Could a schema have a single entry table represented as well?
  // It could be useful to have a different generic type to represent a set<T> vs.
  // a list?
  // --> NOTE: We are using these schema types as a declaration of the overall data,
  // not as a means to resolve that data!
  public List<ExampleParent> Parents { get; set; } = new List<ExampleParent>();
  public List<ExampleChild> Kids { get; set; } = new List<ExampleChild>();
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
  [ChildRelationship]
  public List<ExampleChild> Children { get; set; } = new List<ExampleChild>();
}

// ==========================================================================
public class ExampleChild : IHasPrimary
{
  public int ID { get; set; }
  public string? Label { get; set; }

  // NOTE: This MUST be enforced if the parent has a child relationship.
  // NOT sure how we will go about doing that.....
  // [ParentRelationship]
  // public ExampleParent Parent { get; set; }
}
