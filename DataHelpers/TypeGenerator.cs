﻿using drewCo.Curations;

namespace DataHelpers.Data;

// ============================================================================================================================
public interface IHasPrimary
{
  int ID { get; set; }
}

//// ============================================================================================================================
///// <summary>
///// This indicates that there are child tables that point back to this parent via FK relationship.
///// </summary>
//[Obsolete("This will be removed in favor of 'Relationship' semantics")]
//[AttributeUsage(AttributeTargets.Property)]
//public class ChildRelationship : Attribute
//{
//}


//// ============================================================================================================================
///// <summary>
///// Indicates that the member points to a parent table via FK relationship.
///// </summary>
//[Obsolete("This will be removed in favor of 'Relationship' semantics")]
//[AttributeUsage(AttributeTargets.Property)]
//public class ParentRelationship : Attribute
//{
//}


// ============================================================================================================================
/// <summary>
/// This allows us to have many sets of the same type that can have different names.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class DataSetAttribute : Attribute
{

  // ------------------------------------------------------------------------------------------------
  public DataSetAttribute(string name_)
  {
    this.Name = name_;
  }

  /// <summary>
  /// The name of the data set.
  /// </summary>
  public string Name { get; private set; }

}


// ============================================================================================================================
/// <summary>
/// Describes a relationship to another set of data (table, list, etc.)
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class Relationship : Attribute
{
  // I want an easy way to indicate FK relations in a database, and even a way
  // to indicate many->many type relations....
  // In these cases, we need the name of a Set, or we can use the name of the datatype
  // that this property is attached to.

  // NOTE: I don't think that relationships need to be bi-directional.
  /// <summary>
  /// Name of the data set (TABLE) that this item is related to.
  /// The target data set must have the 'IPrimary' interface.
  /// If not speficied, we will use the name of the PropertyType that this is attached to.
  /// </summary>
  public string? DataSet { get; set; }

  /// <summary>
  /// Name of the target property (if any) in the relationship.
  /// This is how we can explictly define a bi-directional relationship.
  /// </summary>
  public string? TargetProperty { get; set; }

  //// List properties will be: 'ERelType.Many'
  //// Single instances will be: 'ERelType.One'
  //public ERelType RelationshipType { get; set; }


  // NOTE: Maybe instead of attributes, we have an interface 'IHasRelationships' that
  // has a function that returns a list of class instances that describe the relations?
  // That seems a lot more straight forward and easy to resolve.....
  // .... except for the part where we need to map to an instance member....
}


// ============================================================================================================================
public class RelationshipDescription {

    /// <summary>
    /// What kind of relationship are we describging?
    /// </summary>
    public ERelType RelationshipType { get; set; } = ERelType.Invalid;

    /// <summary>
    /// Name of the data set (i.e. TABLE) that this item is related to.
    /// The target data set must have the 'IPrimary' interface.
    /// If not speficied, we will use the name of the PropertyType that this is attached to.
    /// </summary>
    public string? DataSet { get; set; }
}

// ============================================================================================================================
public enum ERelType
{
  Invalid = 0,

  /// <summary>
  /// The entity is associated with one other entity.
  /// </summary>
  One,

  /// <summary>
  /// The entity is associated with many other entities.
  /// </summary>
  Many
}

// ============================================================================================================================
[AttributeUsage(AttributeTargets.Property)]
public class PrimaryKey : Attribute
{
}


// ============================================================================================================================
// NOTE: This might want to go live with reflection tools?
public class TypeGenerator
{
  private object CacheLock = new object();
  private MultiDictionary<Type, Type, Type> _MappingTypesCache = new MultiDictionary<Type, Type, Type>();

  private DynamicTypeManager TypeMan = new DynamicTypeManager("TimeMan_DynamicTypes");

  //// --------------------------------------------------------------------------------------------------------------------------
  //public Type ResolveMappingTableType(Type parentType, Type childType)
  //{
  //  lock (CacheLock)
  //  {
  //    // TODO: Update this call to 'TryGetValue'
  //    if (_MappingTypesCache.ContainsKey(parentType, childType))
  //    {
  //      return _MappingTypesCache[parentType, childType];
  //    }
  //    else
  //    {
  //      // We will now generate the new type definition....
  //      TypeDef tDef = new TypeDef()
  //      {
  //        Name = $"{parentType.Name}_To_{childType.Name}"
  //      };
  //      tDef.Properties.Add(new TypeDef.PropertyDef()
  //      {
  //        Name = parentType.Name,
  //        Type = parentType.Name,
  //        Attributes = new List<TypeDef.AttributeDef>()
  //          {
  //            new TypeDef.AttributeDef(typeof(ChildRelationship))
  //          }
  //      });
  //      tDef.Properties.Add(new TypeDef.PropertyDef()
  //      {
  //        Name = childType.Name,
  //        Type = childType.Name,
  //        Attributes = new List<TypeDef.AttributeDef>()
  //          {
  //            new TypeDef.AttributeDef(typeof(ChildRelationship))
  //          }
  //      });

  //      Type res = TypeMan.CreateDynamicType(tDef);
  //      _MappingTypesCache.Add(parentType, childType, res);
  //      return res;
  //    }
  //  }
  //}


}


//private class DbConnection : IDisposable
//{
//  private SqliteConnection Connection = null;
//  public void Dispose()
//  {
//    Connection.dis
//    throw new NotImplementedException();
//  }
//}
