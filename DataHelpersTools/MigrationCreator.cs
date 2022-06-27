// See https://aka.ms/new-console-template for more information
using System.Reflection;
using DataHelpers.Data;
using DataHelpers.Migrations;
using drewCo.Tools;

// ==========================================================================
interface IFlavorHandler
{
  IDataAccess CreateDataAccess(Type schemaType, string connectionString);
  ISqlFlavor GetFlavor();
}

// ==========================================================================
class SQLiteFlavorHandler : IFlavorHandler
{
  // --------------------------------------------------------------------------------------------------------------------------   
  public IDataAccess CreateDataAccess(Type schemaType, string connectionString)
  {
    string filePath = GetPathFromConnectionString(connectionString);
    string dbDir = Path.GetDirectoryName(filePath)!;
    FileTools.CreateDirectory(dbDir);
    string fileName = Path.GetFileNameWithoutExtension(filePath);

    var dalType = typeof(SqliteDataAccess<>).MakeGenericType(schemaType);
    IDataAccess? dal = (IDataAccess?)Activator.CreateInstance(dalType, new object[] { dbDir, fileName });
    if (dal == null)
    {
      throw new InvalidOperationException($"Could not create an {nameof(IDataAccess)} interface for schema type: {schemaType}");
    }

    return dal;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  private string GetPathFromConnectionString(string connectionString)
  {
    string[] parts = connectionString.Split(";");
    foreach (var p in parts)
    {
      if (p.Trim().ToLower().StartsWith("data source"))
      {
        string[] dsParts = p.Split("=");
        if (dsParts.Length != 2)
        {
          throw new InvalidOperationException($"Could not parse file path from data source part: [{p}]");
        }
        string res = dsParts[1].Trim();
        res = Path.GetFullPath(res);
        return res;
      }
    }

    throw new InvalidOperationException("There is no 'Data Source' part in the connection string!");
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public ISqlFlavor GetFlavor()
  {
    return new SqliteFlavor();
  }
}

// ==========================================================================
internal class MigrationCreator
{
  private CreateMigrationOptions Options;

  private Dictionary<string, IFlavorHandler> FlavorHandlers = new Dictionary<string, IFlavorHandler>();

  // --------------------------------------------------------------------------------------------------------------------------
  public MigrationCreator(CreateMigrationOptions ops)
  {
    this.Options = ops;

    FlavorHandlers.Add("SQLite", new SQLiteFlavorHandler());
  }

  // --------------------------------------------------------------------------------------------------------------------------
  private void ValidateFlavor(string flavor)
  {
    if (!FlavorHandlers.Keys.Any(x => x == flavor))
    {
      string msg = $"The flavor: {flavor} is not supported!" + Environment.NewLine;
      msg += "Valid flavors are:" + Environment.NewLine + string.Join(Environment.NewLine, FlavorHandlers.Keys);
      //      Console.WriteLine(msg);

      // Maybe this exception doesn't have the greatest message....
      throw new ArgumentOutOfRangeException(msg);
    }
  }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Given our options, we can create our migration...
  /// </summary>
  public int Create()
  {
    ValidateFlavor(Options.Flavor);

    // Let's resolve the assembly.
    Assembly asm = ResolveAssembly(Options.AssemblyPath);

    // TODO: This should be part of 'ReflectionTools' probably.
    Type? schemaType = ResolveType(asm, Options.SchemaType);
    if (schemaType == null)
    {
      throw new InvalidOperationException($"There is no type named: {Options.SchemaType} in assmbly: {asm.FullName}");
    }

    // NOTE: Assume SQLite;
    IFlavorHandler flavorHandler = FlavorHandlers[Options.Flavor];
    ISqlFlavor flavor = flavorHandler.GetFlavor();



    // NOTE: Assume there is no 'from' migration.
    DataSchema? fromSchema = null;
    DataSchema toSchema = new DataSchema()
    {
      Version = fromSchema?.Version + 1 ?? 1,
      SchemaDef = new SchemaDefinition(flavor, schemaType),
      Flavor = Options.Flavor
    };

    string useOutputDir = Options.OutputDirectory ?? FileTools.GetAppDir();

    Console.WriteLine("Creating Migration script...");
    var mh = new MigrationHelper();
    Migration m = mh.CreateMigration(fromSchema, toSchema, useOutputDir);

    Console.WriteLine("Applying migration script...");
    var dal = flavorHandler.CreateDataAccess(schemaType, Options.ConnectionString);
    mh.ApplyMigration(m, dal);

    Console.WriteLine("Migration complete!");
    return 0;
  }
  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Resolve a type, by name from a specific assembly.
  /// </summary>
  /// REFACTOR:  This should go in reflacection tools.
  private Type? ResolveType(Assembly asm, string schemaType)
  {
    var types = asm.GetTypes();
    foreach (var t in types)
    {
      if (t.Name == schemaType) { return t; }
    }
    return null;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Like the normal code to resolve an assembly, but allows "." to use the current assembly.
  /// </summary>
  /// <param name="assemblyPath"></param>
  /// <returns></returns>
  private Assembly ResolveAssembly(string assemblyPath)
  {
    if (assemblyPath == ".")
    {
      var res = Assembly.GetExecutingAssembly();
      return res;
    }
    else
    {
      var res = Assembly.LoadFile(assemblyPath);
      return res;
    }
  }

}