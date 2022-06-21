
using System.Data;
using static Dapper.SqlMapper;

namespace DataHelpers.Data;

  // ============================================================================================================================
  /// <summary>
  /// This is a special handler so that we can use DateTimeOffset types with SQLite.
  /// </summary>
  public class DateTimeOffsetHandler : TypeHandler<DateTimeOffset>
  {
    // --------------------------------------------------------------------------------------------------------------------------
    public override DateTimeOffset Parse(object value)
    {
      if (value == null) { return DateTimeOffset.MinValue; }
      if (DateTimeOffset.TryParse(value as string, out DateTimeOffset res))
      {
        return res;
      }
      throw new InvalidOperationException($"Input value: '{value as string}' is not a valid DateTimeOffset type!");
    }

    // --------------------------------------------------------------------------------------------------------------------------
    public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)
    {
      parameter.Value = value;
    }
  }
