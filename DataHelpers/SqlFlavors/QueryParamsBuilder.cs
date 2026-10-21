using System.Text;

namespace DataHelpers.Data
{
  // ============================================================================================================================
  public class QueryParamsBuilder
  {
    private ISqlFlavor Flavor = null!;
    private QueryParams QParams = new Dictionary<string, QueryParamValue>();

    // --------------------------------------------------------------------------------------------------------------------------
    public QueryParamsBuilder(ISqlFlavor flavor_)
    {
      Flavor = flavor_;
    }

    // --------------------------------------------------------------------------------------------------------------------------
    public void Add(string key, object? value, Type? t = null)
    {
      if (key == null) { 
        throw new ArgumentNullException("key may not be null!");
      }

      if (t == null)
      {
        if (value == null) { 
          throw new InvalidOperationException("Illegal! The value and type argument are null!");
        }
        t = value.GetType();
      }

      var qp = new QueryParamValue(value, Flavor.ToDbType(t));
      this.QParams.Add(key, qp);
    }

    // --------------------------------------------------------------------------------------------------------------------------
    public QueryParams Build()
    {
      return QParams;
    }

    // --------------------------------------------------------------------------------------------------------------------------
    /// <summary>
    /// Return the names of all items as a WHERE clause.
    /// </summary>
    public string GetWhereClause()
    {
      var sb = new StringBuilder();
      sb.Append("WHERE ");

      string args = string.Join(" AND ", from x in QParams select GetEqualsExpression(x.Key, x.Value));
      sb.Append(args);

      string res = sb.ToString(); 
      return res;
    }

    // --------------------------------------------------------------------------------------------------------------------------
    private string GetEqualsExpression(string name, QueryParamValue val) { 
      if (val.Value == null)  { 
        return $"{name} IS NULL";
      }
      else { 
        return $"{name} = @{name}";
      }
    }
  }
}
