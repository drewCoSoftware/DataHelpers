
namespace DataHelpers.Data;

// ============================================================================================================================
public class PostgresFlavor : ISqlFlavor
{
  private readonly PostgresDataTypeResolver _TypeResolver = new PostgresDataTypeResolver();
  public IDataTypeResolver TypeResolver { get { return _TypeResolver; } }

  // --------------------------------------------------------------------------------------------------------------------------
  public string GetIdentitySyntax(ColumnDef col)
  {
    if ((col.IsPrimary && col.DataType == "integer" || col.DataType == "bigint"))
    {
      return " GENERATED AS IDENTITY";
    }
    return string.Empty;
  }
}



// ============================================================================================================================
public class PostgresDataTypeResolver : IDataTypeResolver
{
  // private PairDictionary<Type, string> TypeMappings = new PairDictionary<Type, string>()
  // {
  //     { typeof(Int32), ""
  // }

  // --------------------------------------------------------------------------------------------------------------------------
  // NOTE: We might actually want to have more information about the column so we can get the right name...
  public string GetDataTypeName(Type t, bool isPrimaryCol)
  {
    string res = "";

    if (t == typeof(Int32) || t == typeof(Int32?))
    {
      res = isPrimaryCol ? "serial" : "integer";
    }
    else if (t == typeof(Int64))
    {
      res = isPrimaryCol ? "serial" : "bigint";
    }
    else if (t == typeof(float))
    {
      res = "real";
    }
    else if (t == typeof(double) || t == typeof(double?))
    {
      res = "float8";
    }
    else if (t == typeof(string))
    {
      // NOTE: unless we have additional attributes/properties, we won't limit the string lengths....
      // This also implies that we have to pass in that data to this function!
      res = "text";
    }
    else if (t == typeof(DateTimeOffset) ||
             t == typeof(DateTimeOffset?))
    {
      // Postgres also hates making things simple, and has too many date/time options.
      // In this case, we want full date/time/timezone data.
      // NOTE: Always use UTC times like a regular person.  We keep the timezone data for those of us
      // who simply can't/won't care about UTC.
      res = "timestamptz";
    }
    else if (t == typeof(DateTime) ||
          t == typeof(DateTime?))
    {
      res = "date";
    }
    else if (t == typeof(bool))
    {
      // lol, no boolean type either!
      res = "boolean";
    }
    else if (t == typeof(Guid) || t == typeof(Guid?))
    {
      res = "uuid";
    }
    else if (t == typeof(decimal) || t == typeof(decimal?))
    {
      res = "money";
    }
    else
    {
      throw new NotSupportedException($"The data type {t} is not supported!");
    }

    return res;
  }
}