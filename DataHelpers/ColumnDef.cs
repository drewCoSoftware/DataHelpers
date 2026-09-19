using System.Diagnostics;
using System.Reflection;

namespace DataHelpers.Data;

// ============================================================================================================================
[DebuggerDisplay("{PropertyName} : {DataStoreName}")]
public class ColumnDef
{
  /// <summary>
  /// Special DataType name used for placeholder associations defs during schema generation.
  /// </summary>
  public const string ASSOCIATION_PLACEHOLDER = "@_ASSOCIATON";
  public const string COMPOSITE_PLACEHOLDER = "@_COMPOSITE";

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// The name of the property from the source type.  For example the property name for MyClass.TheName is 'TheName', but the data store
  /// name may be different, i.e. 'the_name'.  Nested property names (OtherTable.ID) will be mapped to (OtherTable_ID)
  /// </summary>
  public string? PropertyName { get; private set; }
  public string DataStoreName { get; private set; }                 // The name that is used in the data-store (SQL for example)
  public Type RuntimeType { get; private set; }
  public string DataType { get; private set; }
  public bool IsPrimary { get; private set; }

  [Obsolete("This will be removed in favor of adding indexes to the table.  We may end up providing convenience functions to resolve this however.")]
  public bool IsUnique { get; private set; }
  public bool IsNullable { get; private set; }
  public bool IsComposite { get; private set; }

  public PropertyInfo PropInfo { get; private set; } = null!;

  //// NOTE: This has a non-private setter b/c we have to update them sometimes, after the fact,
  //// because of the sloppy way that we are currently creating the table defs.
  //// we should have it so that the columns are added to the def BEFORE we attempt resolve the associations.
  public AssociatedDatasetInfo? AssociatedDataSet { get; internal set; }

  /// <summary>
  /// The association that is defined for this column.
  /// This data is really only useful when the SchemaDefs are being computed.
  /// </summary>
  internal AssociationAttribute? AssociationDef { get; set; } = null;

  // --------------------------------------------------------------------------------------------------------------------------
  public ColumnDef(string propName, string dataStoreName, Type runtimeType, string dataType, bool isPrimary, bool isUnique, bool isNullable, AssociationAttribute? assocDef_, PropertyInfo? propInfo_, bool isComposite_)
  {
    PropertyName = propName;
    DataStoreName = dataStoreName;
    RuntimeType = runtimeType;
    DataType = dataType;
    IsPrimary = isPrimary;
    IsUnique = isUnique;
    IsNullable = isNullable;
    AssociationDef = assocDef_;

    //if (propInfo_ == null) { 
    //  throw new NullReferenceException("property info is null!");
    //}
    PropInfo = propInfo_;

    IsComposite = isComposite_;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public static bool AreSame(ColumnDef colDef, ColumnDef match)
  {
    bool res = (colDef.PropertyName == match.PropertyName &&
                colDef.IsPrimary == match.IsPrimary &&
                colDef.DataType == match.DataType &&                
                colDef.IsUnique == match.IsUnique);

    return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Tells us if the combination of indexes are the same.
  /// </summary>
  public static bool AreSame(IList<Index> l, IList<Index> r)
  {
    if (l.Count != r.Count) { return false; }
    int len = l.Count;

    // NOTE: Won't a span work better / use less garbage here?
    var matched = new int[len];
    int matchCount = 0;
    for (int i = 0; i < len; i++)
    {
      matched[i] = -1;
    }
    for (int i = 0; i < len; i++)
    {
      for (int j = 0; j < len; j++)
      {
        if (i == j || matched[j] != -1) { continue; }
        bool isMatch = AreSame(l[i], r[j]);
        if (isMatch) { 
          matched[j] = i;
          ++matchCount;
        }
      }
    }

    bool res = matchCount == len;
    return res;

  }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Tells us if the two index definitions are the same.
  /// </summary>
  public static bool AreSame(Index l, Index r)
  {
    if (l.Type != r.Type) { return false; }

    bool res = Enumerable.SequenceEqual(l.Columns.OrderBy(t => t), r.Columns.OrderBy(t => t));
    return res;
  }
}
