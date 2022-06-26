// See https://aka.ms/new-console-template for more information
using CommandLine;


// Command line parser always fucking up.....
int res = Parser.Default.ParseArguments<CreateMigrationOptions>(args)
                        .MapResult((CreateMigrationOptions ops) => CreateMigration(ops)
                        , errs => 1);
return res;

// --------------------------------------------------------------------------------------------------------------------------
int CreateMigration(CreateMigrationOptions ops)
{
  try
  {
    var creator = new MigrationCreator(ops);
    int res = creator.Create();
    return res;
  }
  catch (Exception ex)
  {
    // TODO: Write exception to disk:
    Console.WriteLine(Environment.NewLine);
    Console.WriteLine("Unhandled Exception!");
    Console.WriteLine(ex.Message);
    return 1;
  }
}
