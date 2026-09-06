using drewCo.Curations;
using System.Reflection;

namespace DataHelpers.Data;


// ==============================================================================================================================
/// <summary>
/// Indicates that the type has a primary key.  Reports the type / name of the key.
/// </summary>
public interface IPrimaryKey
{
  public const string DEFAULT_NAME = "ID";

  bool HasDefaultPrimary();
  void SetPrimaryKeyValue(object value);
  Type PrimaryKeyType { get; }
}

// ============================================================================================================================
[Obsolete("Use a PrimaryKey<T> base class!  This interface will be removed!")]
public interface IHasPrimary : IPrimaryKey
{
  int ID { get; set; }

  bool IPrimaryKey.HasDefaultPrimary() { return ID == 0; }
  void IPrimaryKey.SetPrimaryKeyValue(object value) { ID = (int)value; }
  Type IPrimaryKey.PrimaryKeyType { get { return typeof(int); } }
}

// ==============================================================================================================================
/// <summary>
/// Base class to use a primary key of the given type.
/// </summary>
public abstract class PrimaryKey<T> : IPrimaryKey
{
  public T ID { get; set; }

  // --------------------------------------------------------------------------------------------------------------------------
  public bool HasDefaultPrimary()
  {
    bool res = ID.Equals(default(T));
    return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public void SetPrimaryKeyValue(object value)
  {
    ID = (T)value;
  }

  [Ignore]
  public Type PrimaryKeyType { get { return typeof(T); } }

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
  public string DataSetName { get; set; }

  /// <summary>
  /// The name of the property on the defining type that represents the relation.
  /// If null, a default value will be used.
  /// </summary>
  public string? LocalIDPropertyName { get; set; }

  /// <summary>
  /// The name of the property on the target data set that represents the relation.
  /// If null, a default value will be used.
  /// </summary>
  public string? TargetIDPropertyName { get; set; }


  /// <summary>
  /// This is set internally, during schema computation.
  /// </summary>
  internal ERelationType RelationType { get; set; }

  /// <summary>
  /// The instance that stores the actualy relation data.  This may not always be set, depending on the scenario.
  /// </summary>
  public PropertyInfo? TargetProperty { get; set; }

  // --------------------------------------------------------------------------------------------------------------------------
  public RelationAttribute() { }

  // --------------------------------------------------------------------------------------------------------------------------
  public RelationAttribute(string dataSet_)
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
/// <summary>
/// Indicates that the given member is the primary key.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class PrimaryKey : Attribute
{ }


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
