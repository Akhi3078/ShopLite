var builder = DistributedApplication.CreateBuilder(args);

// Catalog's database lives on the developer's local SQL Server instance rather than in a
// container, so it is referenced as an existing connection string. Aspire surfaces it in
// the dashboard and injects it, but does not manage its lifecycle.
//
// The value comes from this project's configuration (appsettings.Development.json).
// Switching to a container later means replacing this one line with:
//     var catalogDb = builder.AddSqlServer("sql").AddDatabase("catalogdb");
var catalogDb = builder.AddConnectionString("catalogdb");

builder.AddProject<Projects.Catalog_Api>("catalog")
    .WithReference(catalogDb);

builder.Build().Run();
