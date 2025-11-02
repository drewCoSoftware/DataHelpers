using drewCo.Curations;

namespace DataHelpers.Data;

// ============================================================================================================================
public interface IHasPrimary
{
  int ID { get; set; }
}


///// <summary>
///// Indicates that this type supports a many-many relationship via mapping table.
///// </summary>
//public interface MappingTable { 
//  public 
//}

// ==============================================================================================================================
[AttributeUsage(AttributeTargets.Class)]
public class MappingTableAttribute : Attribute
{
  //public readonly Type DataSet1Type = null;
  //public readonly Type DataSet2Type = null;
  public readonly string DataSet1 = null!;
  public readonly string DataSet2 = null!;
  public readonly string DataSet1ID = null!;
  public readonly string DataSet2ID = null!;

  // --------------------------------------------------------------------------------------------------------------------------
  public MappingTableAttribute(string dataSet1_, string dataSet2_, string idName1_, string idName2_)
  {
    //DataSet1Type = dataSet1Type_;
    //DataSet2Type = dataSet2Type_;
    DataSet1 = dataSet1_;
    DataSet2 = dataSet2_;
    DataSet1ID = idName1_;
    DataSet2ID = idName2_;
  }
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
public class RelationAttribute : Attribute
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
  public string DataSet { get; set; }

  /// <summary>
  /// The name of the property on the defining type that represents the relation.
  /// If null, a default value will be used.
  /// </summary>
  public string? LocalPropertyName { get; set; }

  /// <summary>
  /// The name of the property on the target data set that represents the relation.
  /// If null, a default value will be used.
  /// </summary>
  public string? TargetPropertyName { get; set; }


  /// <summary>
  /// This is set internally, during schema computation.
  /// </summary>
  internal ERelationType RelationType { get; set; }

  ///// <summary>
  ///// Name of the target property (if any) in the relationship.
  ///// This is how we can explictly define a bi-directional relationship.
  ///// </summary>
  //public string? TargetProperty { get; set; }

  // --------------------------------------------------------------------------------------------------------------------------
  public RelationAttribute() { }

  // --------------------------------------------------------------------------------------------------------------------------
  public RelationAttribute(string dataSet_)
  {
    DataSet = dataSet_;
  }
}


// ============================================================================================================================
public class RelationshipDescription
{

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
