using DataHelpers.Data;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace DataHelpers;


// ==========================================================================
public enum EAssociationType
{
  Invalid = 0,
  Single,
  Many
}

// ==========================================================================
// Used for easy type detection.
public interface IRelation
{ }

// ==========================================================================
// Used for easy type detection.
public interface ISingleAssociation : IRelation
{
  int ID { get; set; }
}

// ==========================================================================
// Used for easy type detection.
public interface IManyAssociation : IRelation
{
}


// ==========================================================================
/// <summary>
/// Represents an FK association to different data set where there can be one or more
/// matches.  The ID property is set on the related table, and may be bi-directional
/// through the use of a 'SingleRelation' instance.
/// </summary>
public class ManyRelation<T> : IManyAssociation
where T : class, new()
{
  public ManyRelation() { }
  public ManyRelation(IEnumerable<T> data)
  {
    this.Data = data.ToList();
  }
  private List<T>? _Data = null;
  public List<T> Data { get { return _Data; } internal set { _Data = value; } }


  /// <summary>
  /// This isn't really an instance, it is a way for us to be able to use the type
  /// in lambda functions.
  /// See test case: CanSelectViaMappingTables for an example of this:
  /// </summary>
  public T Prop { get; set; } = new T();
}



// ==========================================================================
/// <summary>
/// Represents an FK association to a different data set....
/// </summary>
public class SingleRelation<T> : IHasPrimary, ISingleAssociation
where T : class, IHasPrimary
{
  public SingleRelation() { }
  public SingleRelation(int id)
  {
    _ID = id;
  }

  /// <summary>
  /// The ID of the associated entity.
  /// This is the same ID that 'Data' uses!  If the two are out of sync, something is WRONG!
  /// </summary>
  private int _ID = 0;
  public int ID
  {
    get { return _ID; }
    // Setting the ID explicitly will clear the data member!
    set
    {
      _ID = value;
      if (_Data != null && _Data.ID != _ID)
      {
        _Data = null;
      }
    }
  }

  /// <summary>
  /// The data of the associated entity.
  /// It is nullable b/c it may not have been resolved yet...
  /// </summary>
  private T? _Data = null;
  public T? Data
  {
    get { return _Data; }
    // Setting the data explicitly will also change the ID!
    set { _Data = value; _ID = _Data?.ID ?? 0; }
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public static implicit operator SingleRelation<T>(T data)
  {
    var res = new SingleRelation<T>();
    res.Data = data;
    return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public static implicit operator T?(SingleRelation<T> data)
  {
    return data.Data;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public static implicit operator SingleRelation<T>(int id)
  {
    var res = new SingleRelation<T>(id);
    return res;
  }

}


// ==============================================================================================================================
public interface ICompositeSerializer
{
  object Deserialize(string data);
  string Serialize();
}

//// ==============================================================================================================================
//public interface ICompositeSerializer<T> : ICompositeSerializer
//{
//  //Type ICompositeSerializer.GetCompositeType() { return typeof(T); }

//  //T From(string data)
//  //{
//  //  T res = JsonSerializer.Deserialize<T>(data);
//  //  return res;
//  //}
//  //string To()
//  //{
//  //  string res = JsonSerializer.Serialize(this);
//  //  return res;
//  //}
//}

////public class Relations<T> : IHasPrimary {
////  public int 
////}
