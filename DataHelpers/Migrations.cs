
// Migrations are how we create a databse, and how we 'migrate' its data to new versions.
// The intitial migration is creating the database from scratch.
// Subsequent migrations alter that database into different forms.

// Each step of the migration needs a description of the from->to data types.  We could easily
// leverage dType to create both the descriptions, and auto-generate the rules that are needed
// to go from one version to the next.

// I suppose that migration scripts / steps could also be created by hand....


// Here is a rough flow:
// 1. Get current description of database / types.
// 1a. If none, then we can create all of the SQL needed to create the database and tables.

// 2. Get new schema.
// 2a. If there is a current schema, figure out the differences.
// 2b. Generate ALTER syntax.

// 3. For either 1a or 2b, save the migration SQL.
// 4. Save the new (current) schema decription for next migration.

// Migrations should be versioned, 1, 2, 3, etc.
// Always backup your DB before migrating it!