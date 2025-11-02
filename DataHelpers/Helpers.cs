using drewCo.Tools;

namespace DataHelpers.Data;

// ==============================================================================================================================
public static class Helpers {

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Create a set of dynamic query parameters from the given object.
  /// This allows us to use some of our conventions for mapping relationships to types.
  /// </summary>
  public static QueryParams CreateParams(object fromInstance, bool includeID = false)
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
        if (!ReflectionTools.HasInterface<ISingleRelation>(item.PropertyType))
        {
          throw new InvalidOperationException($"All relations should be represented with a {nameof(ISingleRelation)} instance!");
        }

        var relType = item.PropertyType.GetGenericArguments()[0];
        var relVal = item.GetValue(fromInstance);
        if (relVal == null || (relVal as ISingleRelation).ID == 0)
        {
          // This is null, or unset:
          // We will ignore it.  See notes below about constraint enforcements + leaving out values.
          continue;
        }


        string setName = rel.DataSet;
        string useName = setName + "_" + nameof(IHasPrimary.ID);
        int useId = (relVal as ISingleRelation).ID;

        res.Add(useName, useId);
      }
      else
      {
        object? useVal = item.GetValue(fromInstance);
        if (useVal == null)
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