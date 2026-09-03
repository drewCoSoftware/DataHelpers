namespace DataHelpers.Data;

// ============================================================================================================================
/// <summary>
/// Shows that a property on a tabledef should have a unique constraint.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class UniqueAttribute : Attribute
{
  /// <summary>
  /// If set, all columns marked with the same group name will be used to
  /// define a unique index.
  /// If there aren't at least two members of the group, an exception will be thrown.
  /// </summary>
  public string? Group { get; set; } = null;
}


// ==========================================================================
[AttributeUsage(AttributeTargets.Property)]
public class IsNullableAttribute : Attribute
{ }


// ==========================================================================
[AttributeUsage(AttributeTargets.Property)]
public class IgnoreAttribute : Attribute
{ }

