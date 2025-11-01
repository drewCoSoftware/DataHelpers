using DataHelpers.Data;
using System.Runtime.InteropServices;

namespace DataHelpers;

// ==========================================================================
// Used for easy type detection.
public interface ISingleRelation
{
  int ID { get; }
}

// ==========================================================================
/// <summary>
/// Represents an FK relation to a different data set....
/// </summary>
public class SingleRelation<T> : IHasPrimary, ISingleRelation
where T : class, IHasPrimary
{
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
      if (_Data.ID != _ID)
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
  T? Data
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
}

//public class Relations<T> : IHasPrimary {
//  public int 
//}
