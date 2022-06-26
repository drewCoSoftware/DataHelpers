// See https://aka.ms/new-console-template for more information
using CommandLine;


// Command line parser always fucking up.....
int res = Parser.Default.ParseArguments<CreateMigrationOptions>(args)
                        .MapResult((CreateMigrationOptions ops) => CreateMigration(ops)
                        , errs => 1);
return res;

// var ops=  new CreateMigrationOptions()
// {
//   ConnectionString = "Data Source=\"./DB/test.sqlite\";Mode=ReadWriteCreate",
//   AssemblyPath = "./DataHelpersTesters",
//   SchemaType = "TestSchema"
// };
// int res = CreateMigration(ops);
// return res;

int CreateMigration(CreateMigrationOptions ops)
{
  var creator = new MigrationCreator(ops);
  int res = creator.Create();
  return res;
}
