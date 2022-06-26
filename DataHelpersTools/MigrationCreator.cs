// See https://aka.ms/new-console-template for more information
using System.Reflection;
using DataHelpers.Data;
using DataHelpers.Migrations;
using drewCo.Tools;

internal class MigrationCreator
{
  private CreateMigrationOptions Options;

  public MigrationCreator(CreateMigrationOptions ops)
  {
    this.Options = ops;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Given our options, we can create our migration...
  /// </summary>
  public int Create()
  {

    // Let's resolve the assembly.
    Assembly asm = ResolveAssembly(Options.AssemblyPath);

    // TODO: This should be part of 'ReflectionTools' probably.
    Type? schemaType = ResolveType(asm, Options.SchemaType);
    if (schemaType == null)
    {
      throw new InvalidOperationException($"There is no type named: {Options.SchemaType} in assmbly: {asm.FullName}");
    }

    // NOTE: Assume SQLite;
    var flavor = new SqliteFlavor();
    string filePath = GetPathFromConnectionString(Options.ConnectionString);
    string dbDir = Path.GetDirectoryName(filePath)!;
    FileTools.CreateDirectory(dbDir);
    string fileName = Path.GetFileNameWithoutExtension(filePath);



    // NOTE: Assume there is no 'from' migration.
    DataSchema? fromSchema = null;
    DataSchema toSchema = new DataSchema()
    {
      Version = fromSchema?.Version + 1 ?? 1,
      SchemaDef = new SchemaDefinition(flavor, schemaType)
    };

    Console.WriteLine("Creating Migration script...");
    var mh = new MigrationHelper();
    Migration m = mh.CreateMigration(fromSchema, toSchema);

    Console.WriteLine("Applying migration script...");
    var dalType = typeof(SqliteDataAccess<>).MakeGenericType(schemaType);
    IDataAccess? dal = (IDataAccess?)Activator.CreateInstance(dalType, new object[] { dbDir, fileName });
    if (dal == null)
    {
      throw new InvalidOperationException($"Could not create an {nameof(IDataAccess)} interface for schema type: {schemaType}");
    }
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
}