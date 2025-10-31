using DataHelpers.Data;

namespace DataHelpers;

// ==========================================================================
/// <summary>
/// Represents an FK relation to a different data set....
/// </summary>
public class Relation<T> : IHasPrimary
where T : IHasPrimary
{
  /// <summary>
  /// The ID of the associated entity.
  /// This is the same ID that 'Data' uses!  If the two are out of sync, something is WRONG!
  /// </summary>
  public int ID { get; set; }

  /// <summary>
  /// The data of the associated entity.
  /// It is nullable b/c it may not have been resolved yet...
  /// </summary>
  T? Data { get; set; }
}
