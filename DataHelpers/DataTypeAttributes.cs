namespace DataHelpers.Data;

// ============================================================================================================================
/// <summary>
/// Shows that a property on a tabledef should have a unique constraint.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class UniqueAttribute : Attribute
{ }


// ==========================================================================
[AttributeUsage(AttributeTargets.Property)]
public class IsNullableAttribute : Attribute
{ }