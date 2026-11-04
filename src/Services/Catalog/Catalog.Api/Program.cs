using Catalog.Api.Features.Products;
using Catalog.Api.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry, health checks, service discovery and HTTP resilience.
// Every service in the solution calls this and nothing else for cross-cutting concerns.
builder.AddServiceDefaults();

// Aspire's EF Core integration. Beyond registering the DbContext it adds a readiness
// health check, connection retry on transient faults, and database telemetry, all keyed
// off the connection string named "catalogdb" supplied by the AppHost.
builder.AddSqlServerDbContext<CatalogDbContext>("catalogdb");

builder.Services.AddOpenApi();

// Unhandled exceptions become RFC 7807 responses rather than HTML error pages.
builder.Services.AddProblemDetails();

var app = builder.Build();

// /alive  — liveness: is this process responsive?
// /health — readiness: is it responsive AND can it reach SQL Server?
app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    await app.Services.SeedCatalogAsync();
}

app.MapProducts();

app.Run();
