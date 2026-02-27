using System.Reflection;

namespace DataHelpers.Data;

// ============================================================================================================================
public class ColumnDef
{
  /// <summary>
  /// Special DataType name used for placeholder relation defs during schema generation.
  /// </summary>
  public const string RELATION_PLACEHOLDER = "@_RELATION";

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
  public bool IsUnique { get; private set; }
  public bool IsNullable { get; private set; }

  public PropertyInfo? PropInfo { get; private set; } = null;

  //// NOTE: This has a non-private setter b/c we have to update them sometimes, after the fact,
  //// because of the sloppy way that we are currently creating the table defs.
  //// we should have it so that the columns are added to the def BEFORE we attempt resolve the relationships.
  public RelatedDatasetInfo? RelatedDataSet { get; internal set; }

  /// <summary>
  /// The relationship that is defined for this column.
  /// This data is really only useful when the SchemaDefs are being computed.
  /// </summary>
  internal RelationAttribute? RelationDef { get; set; } = null;

  // --------------------------------------------------------------------------------------------------------------------------
  public ColumnDef(string propName, string dataStoreName, Type runtimeType, string dataType, bool isPrimary, bool isUnique, bool isNullable, RelationAttribute? relationDef_, PropertyInfo? propInfo_)
  {
    PropertyName = propName;
    DataStoreName = dataStoreName;
    RuntimeType = runtimeType;
    DataType = dataType;
    IsPrimary = isPrimary;
    IsUnique = isUnique;
    IsNullable = isNullable;
    RelationDef = relationDef_;
    PropInfo = propInfo_;
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
}
