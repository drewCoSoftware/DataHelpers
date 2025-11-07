using drewCo.Tools;
using drewCo.Tools.Logging;

namespace DataHelpers.Data;

// ==============================================================================================================================
public static class Helpers
{

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

      var rel = ReflectionTools.GetAttribute<RelationAttribute>(item);
      if (rel != null)
      {
        // We need to support only those 
        if (ReflectionTools.HasInterface<ISingleRelation>(item.PropertyType))
        {
          string setName = rel.DataSetName;
          string useName = setName + "_" + nameof(IHasPrimary.ID);

          var relType = item.PropertyType.GetGenericArguments()[0];
          var relVal = item.GetValue(fromInstance);
          if (relVal == null || (relVal as ISingleRelation).ID == 0)
          {
            // This is null, or unset:
            // We will ignore it.  See notes below about constraint enforcements + leaving out values.
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