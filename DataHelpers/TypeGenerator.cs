using drewCo.Curations;
using System.Net;
using System.Reflection;
using System.Runtime.Intrinsics.Arm;

namespace DataHelpers.Data;

// ============================================================================================================================
public interface IHasPrimary
{
  int ID { get; set; }
}


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
public class AssociationDef
{
  // --------------------------------------------------------------------------------------------------------------------------
  public AssociationDef(TableDef targetSet_)
  {
    if (targetSet_ == null) { throw new ArgumentNullException($"argument: {nameof(targetSet_)} is null!"); }
    TargetSet = targetSet_;
  }

  /// <summary>
  /// The target data set that this association points to.
  /// </summary>
  public TableDef TargetSet { get; private set; }
  
  /// <summary>
  /// The field of the ID in the target data set.
  /// </summary>
  public ColumnDef TargetIDField { get; set; }

  /// <summary>
  /// The property on the data type that defines the association (ISingle*, IMany*, etc.)
  /// </summary>
  public PropertyInfo? AssociationProperty { get; set; }

  /// <summary>
  /// What kind of association is this?
  /// </summary>
  public EAssociationType AssociationType { get; set; }

  /// <summary>
  /// Indicates that associations to the target set are required.  In a database, this would be a nullable foreign key.
  /// </summary>
  public bool IsOptional { get; set; }

  [Obsolete("this should be replaced with 'local field' but probably only for singel associaitons.")]
  public string? LocalIDPropertyName { get; set; }

  [Obsolete("We are going to only use 'LocalIDPropertyName' in the future!")]
  public string? TargetIDPropertyName { get; set; }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// This tells us if we have a dependency on the given table, anywhere in the chain....
  /// </summary>
  internal bool HasTableDependency(TableDef toDataSet)
  {
    if (IsOptional) { return false; }
    foreach (var dep in this.TargetSet.AssociationDefs)
    {
      if (!dep.IsOptional && dep.TargetSet.DataType == toDataSet.DataType)
      {
        return true;
      }
    }

    return false;
  }
}

// ============================================================================================================================
/// <summary>
/// Describes a relationship to another set of data (table, list, etc.)
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class AssociationAttribute : Attribute
{
  // I want an easy way to indicate FK associations in a database, and even a way
  // to indicate many->many type relations....
  // In these cases, we need the name of a Set, or we can use the name of the datatype
  // that this property is attached to.

  // NOTE: I don't think that relationships need to be bi-directional.
  /// <summary>
  /// Name of the data set (TABLE) that this item is related to.
  /// The target data set must have the 'IPrimary' interface.
  /// If not speficied, we will use the name of the PropertyType that this is attached to.
  /// </summary>
  public string DataSetName { get; set; }

  /// <summary>
  /// The name of the association.  This can be used to Make it clear that a single->many association
  /// is mutual, which avoids creating redundant FK (REF) fields.
  /// </summary>
  public string? AssociationName { get; set; }

  ///// <summary>
  ///// The name of the property on the target data set that represents the association.
  ///// If null, a default value will be used.
  ///// </summary>
  //[Obsolete]
  //public string? TargetIDPropertyName { get; set; }


  /// <summary>
  /// This is set internally, during schema computation.
  /// </summary>
  [Obsolete("this will be removed!")]
  internal EAssociationType AssociationType { get; set; }

  /// <summary>
  /// The instance that stores the actualy association data.  This may not always be set, depending on the scenario.
  /// </summary>
  [Obsolete("this will be removed!")]
  public PropertyInfo? TargetProperty { get; set; }

  // --------------------------------------------------------------------------------------------------------------------------
  public AssociationAttribute() { }

  // --------------------------------------------------------------------------------------------------------------------------
  public AssociationAttribute(string dataSet_)
  {
    DataSetName = dataSet_;
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
