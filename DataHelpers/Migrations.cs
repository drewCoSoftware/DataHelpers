
//// Migrations are how we create a databse, and how we 'migrate' its data to new versions.
//// The intitial migration is creating the database from scratch.
//// Subsequent migrations alter that database into different forms.

//// Each step of the migration needs a description of the from->to data types.  We could easily
//// leverage dType to create both the descriptions, and auto-generate the rules that are needed
//// to go from one version to the next.

//// I suppose that migration scripts / steps could also be created by hand....


//// Here is a rough flow:
//// 1. Get current description of database / types.
//// 1a. If none, then we can create all of the SQL needed to create the database and tables.

//// 2. Get new schema.
//// 2a. If there is a current schema, figure out the differences.
//// 2b. Generate ALTER syntax.

//// 3. For either 1a or 2b, save the migration SQL.
//// 4. Save the new (current) schema decription for next migration.

//// Migrations should be versioned, 1, 2, 3, etc.
//// Always backup your DB before migrating it!
//using System.Text;
//using System.Text.Json;
//using System.Text.Json.Serialization;
//using DataHelpers.Data;
//using drewCo.Tools;

//namespace DataHelpers.Migrations;

//// ==========================================================================
//// Dummy class to represent the current schema.
//public class DataSchema
//{
//  public string Flavor { get; set; }
//  public int Version { get; set; } = 1;

//  // NOTE: We need an actual description of the types + their relationship.
//  // SchemaDefinition *might* work, but we will have to find a proper way to serialize its data
//  // if that is true.  
//  // ?? Is there some way to create a custom serializer for certian types using jsonSErializer ??
//  [JsonIgnore]
//  public SchemaDefinition SchemaDef { get; set; }   
//}

//// ==========================================================================
///// <summary>
///// The script that is generated to run the migration.
///// </summary>
///// <remarks>
///// At this time we are just wrapping a sql string...
///// Not really sure how to do a series of statements in something like SQLite or Portgres, etc.
///// From what I can tell, it is just the semicolon for sqlite.
///// </remarks>
//public class MigrationScript
//{
//  public string SQL { get; set; } = string.Empty;
//}

//// ==========================================================================
//public record class Migration(DataSchema? From, DataSchema To, MigrationScript Script, string SchemaFilePath);

//// ==========================================================================
//public class MigrationHelper<TSchema>
//{
//  // --------------------------------------------------------------------------------------------------------------------------
//  /// <summary>
//  /// 
//  /// </summary>
//  /// <param name="from"></param>
//  /// <param name="to"></param>
//  /// <param name="outputDir">Directory that the new migration state file will be saved to.</param>
//  public Migration CreateMigration(DataSchema? from, DataSchema to, string outputDir)
//  {
//    var script = new MigrationScript();
//    if (from == null)
//    {
//        // We only need to add 'CREATE' type syntax for the tables.
//        var sb= new StringBuilder();
//        foreach(var def in to.SchemaDef.TableDefs)
//        {
//            sb.Append($"-- TABLE: {def.Name}");
//            sb.Append(Environment.NewLine);
//            sb.Append(def.GetCreateQuery());
//            sb.Append(Environment.NewLine);
//        }

//        script.SQL = sb.ToString();
//    }
//    else
//    {
//      throw new NotSupportedException("ALTER type migrations are not supported at this time!");
//    }

//    FileTools.CreateDirectory(outputDir);
//    string schemaDataPath= Path.Combine(outputDir, $"Migration_{to.Flavor}_{to.Version}.json");


//    string schemaData = JsonSerializer.Serialize(to, new JsonSerializerOptions()
//    {
//      WriteIndented = true
//    });
//    File.WriteAllText(schemaDataPath, schemaData);


//    var res = new Migration(from, to, script, schemaDataPath);
//    return res;
//  }

//  // --------------------------------------------------------------------------------------------------------------------------
//  /// <summary>
//  /// Apply the given migration against a target database.
//  /// </summary>
//  public void ApplyMigration(Migration migration, IDataAccess<TSchema> dataAccess)
//  {
//      if (migration?.Script?.SQL == null)
//      { 
//        throw new ArgumentNullException("The migration and or migration script is null!");
//      }

//      dataAccess.RunExecute(migration.Script.SQL, null);


//  }

//}