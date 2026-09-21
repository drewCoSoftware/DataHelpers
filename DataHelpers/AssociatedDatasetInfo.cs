namespace DataHelpers.Data;

// ============================================================================================================================
/// <summary>
/// Describes a table that another is dependent upon.
/// This is your typical Foreign Key relationship in an RDBMS system.
/// </summary>
public class AssociatedDatasetInfo
{
  public TableDef TargetSet { get; set; }
  public EAssociationType RelationType { get; set; }

  /// <summary>
  /// The name of the property that contains the table in question.
  /// </summary>
  /// <remarks>This only applies to child data sets.</remarks>
  public string DataStoreName { get; set; } = string.Empty;

  public ColumnDef TargetIDColumn { get; set; } = null!;

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// This tells us if we have a dependency on the given table, anywhere in the chain....
  /// </summary>
  internal bool HasTableDependency(TableDef t)
  {
    foreach (var dep in this.TargetSet.RelatedDataSets)
    {
      if (dep.TargetSet.DataType == t.DataType)
      {
        return true;
      }
      //if (dep.HasTableDependency(t))
      //{
      //  return true;
      //}
    }

    return false;
  }
}
