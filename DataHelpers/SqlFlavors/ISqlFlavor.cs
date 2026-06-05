using drewCo.Tools.Logging;
using drewCo.Tools;
using ClankerCode;
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
    public void Add(string key, object value, Type? t = null)
    {
      if (t == null)
      {
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

      string args = string.Join(" AND ", from x in QParams select $"{x.Key} = @{x.Key}");
      sb.Append(args);

      string res = sb.ToString(); 
      return res;
    }

  }

  // ============================================================================================================================
  /// <summary>
  /// Interface to help us deal with the difference between different SQL languages.
  /// Ideally we want a single API in our applications so that we can swap data providers on the fly.
  /// </summary>
  public interface ISqlFlavor : IDbTypeMapper
  {
    IDataTypeResolver TypeResolver { get; }

    /// <summary>
    /// Compute the name that will be used on the data store (typically sql)
    /// for this property.
    /// </summary>
    string GetDataStoreName(string propName)
    {
      string res = propName.ToLower();
      return res;
    }

    /// <summary>
    /// Some flavors want to declare their refs as part of the column def.
    /// </summary>
    bool UsesInlineFKDeclaration { get; }


    public string GetIdentitySyntax(ColumnDef col);

    public string TrueValue { get; }
    public string FalseValue { get; }

    // --------------------------------------------------------------------------------------------------------------------------
    // REFACTOR -> 'CreateParams'
    public QueryParams? ResolveQueryParams(object? qParams)
    {
      QueryParams? useParams = null;
      if (qParams != null)
      {
        if (qParams is QueryParams)
        {
          useParams = qParams as QueryParams;
        }
        else
        {
          useParams = CreateParams(qParams);
        }
      }

      return useParams;
    }

    // --------------------------------------------------------------------------------------------------------------------------
    /// <summary>
    /// Create a set of dynamic query parameters from the given object.
    /// This allows us to use some of our conventions for mapping relationships to types.
    /// </summary>
    QueryParams CreateParams(object fromInstance, bool includeNulls = false, bool includeID = false)
    {
      if (fromInstance == null) { throw new ArgumentNullException($"Please provide an instance for {nameof(fromInstance)}"); }

      // var res = new QueryParams();
      var qpb = new QueryParamsBuilder(this);

      var t = fromInstance.GetType();
      var props = ReflectionTools.GetProperties(t);
      foreach (var prop in props)
      {
        // Don't attempt to include ids.
        if (prop.Name == nameof(IHasPrimary.ID) && !includeID) { continue; }
        if (ReflectionTools.HasAttribute<IgnoreAttribute>(prop)) { continue; }

        var relAttr = ReflectionTools.GetAttribute<RelationAttribute>(prop);
        if (relAttr != null)
        {
          if (ReflectionTools.HasInterface<ISingleRelation>(prop.PropertyType))
          {
            string setName = relAttr.DataSetName;
            string useName = relAttr.LocalIDPropertyName ?? setName + "_" + nameof(IHasPrimary.ID);

            var relType = prop.PropertyType.GetGenericArguments()[0];
            var relVal = prop.GetValue(fromInstance);
            if (relVal == null || (relVal as ISingleRelation).ID == 0)
            {
              // TODO: If the property isn't nullable, we should raise a flag here!
              // Not sure if we should blow it up, but I will for now....
              // NOTE: This call isn't detecting the nullability of the type correctly!
              //if (!TableDef.IsNullableEx(item)) { 
              //  throw new Exception("The value for a non-nullable property is currently null!");
              //}

              // This is null, or unset:
              if (includeNulls)
              {
                qpb.Add(useName, null, typeof(int)); 
                // res.Add(useName, new QueryParamValue(null, ToDbType(typeof(int))));
              }
              continue;
            }


            int useId = (relVal as ISingleRelation).ID;
            qpb.Add(useName, useId, typeof(int));
            // res.Add(useName, new QueryParamValue(useId, ToDbType(typeof(int))));
          }
          else if (ReflectionTools.HasInterface<IManyRelation>(prop.PropertyType))
          {
            // TODO: Decide what to do about this.  In this case, there could be many related instances
            // each with their own ID, etc.....

            // Scenario one:
            // A one -> many relationship just means that some other Dataset has an FK to this one.
            // In that case, there is nothing for us to include, esp. if this is an INSERT query.
            // TODO: We don't have any indication as to what type of query we are creating params for,
            // so we should look into it at some point.
            var manyVal = prop.GetValue(fromInstance);
            if (manyVal == null)
            {
              // There is no data anyway, so we can skip.
              continue;
            }
            Log.Warning("There is currently no support for many relations!");
            continue;
          }
          else
          {
            throw new InvalidOperationException($"All relations should be represented with a {nameof(ISingleRelation)} OR {nameof(IManyRelation)} instance!");
          }

        }
        else
        {
          object? useVal = prop.GetValue(fromInstance);

          // NOTE: Why does the value work for sqlite....
          if (useVal != null && prop.PropertyType.IsEnum)
          {
            if (useVal.GetType() == typeof(string))
            {
              useVal = Enum.Parse(prop.PropertyType, (string)useVal);
            }
            else
            {
              useVal = (int)useVal;
            }
          }

          if (!includeNulls && useVal == null)
          {
            // NOTE: Depending on what we are doing, and what data set / type we are targeting, we may
            // want to flag non-nullable values.  Requires more machinery, but might be nice....
            continue;
          }

          bool isComposite = ReflectionTools.HasInterface<ICompositeSerializer>(prop.PropertyType);
          if (isComposite)
          {
            // var cs = useVal as ICompositeSerializer;
            // var genFunc = typeof(ICompositeSerializer<>).MakeGenericType(new[] { cs.GetCompositeType() }).GetMethod("To");
            useVal = (useVal as ICompositeSerializer).Serialize(); // (string)genFunc.Invoke(useVal, null);

            // useVal = cs.ToS
          }
          qpb.Add(prop.Name, useVal, prop.PropertyType);
          // res.Add(prop.Name, new QueryParamValue(useVal, ToDbType(prop.PropertyType)));
        }
      }

      var res = qpb.Build();
      return res;
    }

  }


  // ============================================================================================================================
  public interface IDataTypeResolver
  {
    public const string INTEGER = "INTEGER";
    public const string TEXT = "TEXT";

    // NOTE: Sometimes we have to know if we are dealing with a primary key or not.
    string GetDataTypeName(Type t, bool isPrimaryCol);
  }
}
