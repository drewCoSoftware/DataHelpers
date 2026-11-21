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
/// <summary>
/// Specifies a database column default. The value's type must match the property's type.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class DefaultValueAttribute : Attribute
{
  private object? _Value;
  public object? Value { get { return _Value; } set { _Value = value; } }
}


// ==========================================================================
[AttributeUsage(AttributeTargets.Property)]
public class IsNullableAttribute : Attribute
{ }


// ==========================================================================
[AttributeUsage(AttributeTargets.Property)]
public class IgnoreAttribute : Attribute
{ }

