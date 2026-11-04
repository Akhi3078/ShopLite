using Catalog.Api.Endpoints;
using Catalog.Application;
using Catalog.Infrastructure;
using Catalog.Infrastructure.Persistence;
using Scalar.AspNetCore;
using ShopLite.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry, health checks, service discovery and HTTP resilience.
// Every service in the solution calls this and nothing else for cross-cutting concerns.
builder.AddServiceDefaults();

// Validates JWTs minted by the Identity service and defines the shared role policies.
// Catalog never calls Identity to do this — the token is signed, so it is self-contained.
builder.AddShopLiteAuthentication();

// The browser calls this API directly from a different origin during development.
builder.AddShopLiteCors();

// The composition root: this is the only project that knows all the layers exist.
builder.AddCatalogInfrastructure();
builder.Services.AddCatalogApplication();

builder.Services.AddOpenApi();

// Unhandled exceptions become RFC 7807 responses rather than HTML error pages.
builder.Services.AddProblemDetails();

var app = builder.Build();

// /alive  — liveness: is this process responsive?
// /health — readiness: is it responsive AND can it reach SQL Server?
app.MapDefaultEndpoints();

// CORS before auth, so a rejected preflight still carries the right headers.
app.UseShopLiteCors();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    await app.Services.SeedCatalogAsync();
}

app.MapProducts();

app.Run();
