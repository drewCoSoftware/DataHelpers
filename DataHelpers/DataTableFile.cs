using drewCo.Tools;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DataHelpers.Data;

// ============================================================================================================================
/// <summary>
/// /// This is meant to mimick a table in a database, however this one is stored
/// on disk.
/// </summary>
public class DataTableFile<T> where T : class, IHasPrimary, new()
{
  private object IDLock = new object();
  private object AddLock = new object();
  private object WriteLock = new object();

  private List<T> Items { get; set; } = new List<T>();
  private int LastID { get; set; } = 0;

  [JsonIgnore]
  public DateTime LastWrite { get; private set; } = DateTime.MinValue;

  private string FilePath;
  private string DataDir;

  // --------------------------------------------------------------------------------------------------------------------------
  public DataTableFile(string dataDir)
  {
    DataDir = dataDir;
    FilePath = Path.Combine(DataDir, "datatablefile.json");
    if (File.Exists(FilePath))
    {
      // Load our content from disk!
      string data = File.ReadAllText(FilePath);
      var doc = JsonDocument.Parse(data);
      var root = doc.RootElement;

      {
        var prop = root.GetProperty(nameof(LastID));
        LastID = prop.GetInt32();
      }


      {
        var prop = root.GetProperty(nameof(Items));
        foreach (var item in prop.EnumerateArray())
        {
          T nextItem = item.Deserialize<T>();
          Items.Add(nextItem);
        }
      }

      UpdateWriteTime();
    }
    else
    {
      // This is a new file, I guess....
    }
  }

  // --------------------------------------------------------------------------------------------------------------------------
  private void UpdateWriteTime()
  {
    var fi = new FileInfo(FilePath);
    this.LastWrite = fi.LastWriteTimeUtc;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public T? GetItem(int id)
  {
    // NOTE: This is really slow.  An index is the answer, but we can implement something like that later.
    // I don't think that our data file formats are really useful for that type of thing....
    foreach (var item in this.Items)
    {
      if (item.ID == id) { return item; }
    }
    return null;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public void AddItem(T item)
  {
    if (item.ID != 0)
    {
      throw new InvalidOperationException("Item has already been added (id != 0)");
    }
    lock (AddLock)
    {
      int id = GetNextID();
      item.ID = id;
      Items.Add(item);

      Save();
    }
  }

  // --------------------------------------------------------------------------------------------------------------------------
  private void Save()
  {
    lock (WriteLock)
    {
      // Check for correct write times.....
      if (File.Exists(FilePath))
      {
        var fi = new FileInfo(FilePath);
        if (this.LastWrite != fi.LastWriteTimeUtc)
        {
          // TODO: This needs a proper exception type so we can implement retries elsewhere....
          throw new InvalidOperationException("File write times don't match!  Can't save!");
        }
      }
      else
      {
        FileTools.CreateDirectory(Path.GetDirectoryName(FilePath));
      }

      // An anonymous proxy object ought to do it...
      string data = JsonSerializer.Serialize(new { LastID = this.LastID, Items = this.Items });
      File.WriteAllText(FilePath, data);
    }
  }


  // --------------------------------------------------------------------------------------------------------------------------
  private int GetNextID()
  {
    lock (IDLock)
    {
      ++LastID;
      int res = LastID;
      return res;
    }
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public List<T> GetItems()
  {
    // Yes, this is also a pig....
    // We are making a deep copy of everything and sending it back.
    // In a perfect world, we would have a query type interface to do this.
    var res = new List<T>();
    foreach (var item in this.Items)
    {
      res.Add(DTOMapper.CreateCopy<T>(item));
    }
    return res;
  }
}
