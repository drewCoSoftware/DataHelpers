
using CommandLine;

// ==========================================================================
[Verb("migrate", HelpText = "Create a new migration")]
class CreateMigrationOptions
{
  /// <summary>
  /// Connection string to the database that contains the schema.
  /// </summary>
  [Option("connection-string", Required = true)]
  public string ConnectionString { get; set; }

  [Option("old-schema-file", Required =false, HelpText = "Path to the current schema file.  This is needed to create ALTER type migrations.")]
  public string OldSchemaFile { get; set; }

  /// <summary>
  /// Path to the assembly that contains the data-type to create the new schema from.
  /// </summary>
  /// <value></value>
  [Option("assembly-path", Required = true)]
  public string AssemblyPath { get; set; }

  /// <summary>
  /// Name of the data type to create the migration from.
  /// </summary>
  [Option("data-type", Required = true)]
  public string SchemaType { get; set; }

  [Option("flavor", Required = true, HelpText = "The SQL flavor that the migration should be created for.  The old schema flavor must match this value!")]
  public string Flavor { get; set; }

  [Option("output-dir", Required = false, HelpText = "Location where migration data will be written.")]
  public string? OutputDirectory { get; set; } = null;
}








// ==========
// Junk for my own command-line parsing code, which I am not going to write at this point...
// // ==========================================================================
// /// <summary>
// /// Describes a verb that can be used in a command line argument.
// /// </summary>
// public class VerbEx : Attribute
// {
//   public string Name { get; private set; }
//   public Verb(string name_)
//   {
//     Name = name_;
//   }
// }

// // ==========================================================================
// public enum EOptionType
// {
//   Invalid = 0,

//   // The option takes a single parameter.
//   Value,

//   // Boolean options appear and don't have an additional parameter.
//   Boolean
// }

// // ==========================================================================
// /// <summary>
// /// An option that can be applied to a verb.
// /// </summary>
// public class OptionEx : Attribute
// {
//   public string Name { get; private set; }
//   public string Group { get; set; } = string.Empty;

//   /// <summary>
//   /// Text used to describe what the option is for.
//   /// </summary>
//   public string HelpText { get; set; } = string.Empty;

//   // /// <summary>
//   // /// Used to describe a positional parameter.  
//   // /// </summary>
//   // /// <value></value>
//   // public int Position { get; set; } = -1;

//   public Option(string name_)
//   {
//     Name = name_;
//   }

//   public EOptionType OptionType { get; internal set; } = EOptionType.Invalid;
// }
