using drewCo.Curations;

namespace DataHelpers.Data;

// ============================================================================================================================
public interface IHasPrimary
{
  int ID { get; set; }
}

// ============================================================================================================================
/// <summary>
/// This indicates that there are child tables that point back to this parent via FK relationship.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class ChildRelationship : Attribute
{
}

/// <summary>
/// Indicates that the member points to a parent table via FK relationship.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class ParentRelationship : Attribute
{
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

  // --------------------------------------------------------------------------------------------------------------------------
  public Type ResolveMappingTableType(Type parentType, Type childType)
  {
    lock (CacheLock)
    {
      // TODO: Update this call to 'TryGetValue'
      if (_MappingTypesCache.ContainsKey(parentType, childType))
      {
        return _MappingTypesCache[parentType, childType];
      }
      else
      {
        // We will now generate the new type definition....
        TypeDef tDef = new TypeDef()
        {
          Name = $"{parentType.Name}_To_{childType.Name}"
        };
        tDef.Properties.Add(new TypeDef.PropertyDef()
        {
          Name = parentType.Name,
          Type = parentType.Name,
          Attributes = new List<TypeDef.AttributeDef>()
            {
              new TypeDef.AttributeDef(typeof(ChildRelationship))
            }
        });
        tDef.Properties.Add(new TypeDef.PropertyDef()
        {
          Name = childType.Name,
          Type = childType.Name,
          Attributes = new List<TypeDef.AttributeDef>()
            {
              new TypeDef.AttributeDef(typeof(ChildRelationship))
            }
        });

        Type res = TypeMan.CreateDynamicType(tDef);
        _MappingTypesCache.Add(parentType, childType, res);
        return res;
      }
    }
  }


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
