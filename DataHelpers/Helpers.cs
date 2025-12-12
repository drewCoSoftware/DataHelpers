using drewCo.Tools;
using drewCo.Tools.Logging;

namespace DataHelpers.Data;

// ==============================================================================================================================
public static class Helpers
{

  // --------------------------------------------------------------------------------------------------------------------------
  [Obsolete("Use version from drewCo.Tools > 1.4.1.0")]
  public static string GetFirstWord(string query)
  {
    string res = query;
    int firstSpace = query.IndexOf(' ');
    if (firstSpace != -1)
    {
      res = query.Substring(0, firstSpace);
    }

    return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public static QueryParams? ResolveQueryParams(object? qParams, string queryType)
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
        useParams = Helpers.CreateParams(queryType, qParams);
      }
    }

    return useParams;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Create a set of dynamic query parameters from the given object.
  /// This allows us to use some of our conventions for mapping relationships to types.
  /// </summary>
  public static QueryParams CreateParams(string queryType, object fromInstance, bool includeNulls = false, bool includeID = false)
  {
    if (fromInstance == null) { throw new ArgumentNullException($"Please provide an instance for {nameof(fromInstance)}"); }

    var res = new QueryParams();

    var t = fromInstance.GetType();
    var props = ReflectionTools.GetProperties(t);
    foreach (var item in props)
    {
      // Don't attempt to include ids.
      if (item.Name == nameof(IHasPrimary.ID) && !includeID) { continue; }

      var relAttr = ReflectionTools.GetAttribute<RelationAttribute>(item);
      if (relAttr != null)
      {
        if (ReflectionTools.HasInterface<ISingleRelation>(item.PropertyType))
        {
          string setName = relAttr.DataSetName;
          string useName = relAttr.LocalIDPropertyName ?? setName + "_" + nameof(IHasPrimary.ID);

          var relType = item.PropertyType.GetGenericArguments()[0];
          var relVal = item.GetValue(fromInstance);
          if (relVal == null || (relVal as ISingleRelation).ID == 0)
          {
            // TODO: If the property isn't nullable, we should raise a flag here!
            // Not sure if we should blow it up, but I will for now....
            // NOTE: This call isn't detecting the nullability of the type correctly!
            //if (!TableDef.IsNullableEx(item)) { 
            //  throw new Exception("The value for a non-nullable property is currently null!");
            //}

            // This is null, or unset:
            if (includeNulls) { 
              res.Add(useName, null);
            }
            continue;
          }


          int useId = (relVal as ISingleRelation).ID;

          res.Add(useName, useId);
        }
        else if (ReflectionTools.HasInterface<IManyRelation>(item.PropertyType))
        {
          // TODO: Decide what to do about this.  In this case, there could be many related instances
          // each with their own ID, etc.....

          // Scenario one:
          // A one -> many relationship just means that some other Dataset has an FK to this one.
          // In that case, there is nothing for us to include, esp. if this is an INSERT query.
          // TODO: We don't have any indication as to what type of query we are creating params for,
          // so we should look into it at some point.
          var manyVal = item.GetValue(fromInstance);
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
        object? useVal = item.GetValue(fromInstance);
        if (!includeNulls && useVal == null)
        {
          // NOTE: Depending on what we are doing, and what data set / type we are targeting, we may
          // want to flag non-nullable values.  Requires more machinery, but might be nice....
          continue;
        }
        res.Add(item.Name, useVal);
      }
    }

    return res;

    // throw new NotImplementedException();
  }

}