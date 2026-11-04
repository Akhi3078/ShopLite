using Catalog.Application.Abstractions;
using Catalog.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Catalog.Infrastructure;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddCatalogInfrastructure(this IHostApplicationBuilder builder)
    {
        // Aspire's EF Core integration. Beyond registering the DbContext it adds a
        // readiness health check, connection retry on transient faults, and database
        // telemetry, keyed off the connection string named "catalogdb".
        builder.AddSqlServerDbContext<CatalogDbContext>("catalogdb");

        // The Application layer asks for ICatalogDbContext; this is where that promise
        // is kept. Resolving the same scoped instance keeps one change tracker per request.
        builder.Services.AddScoped<ICatalogDbContext>(
            serviceProvider => serviceProvider.GetRequiredService<CatalogDbContext>());

        return builder;
    }
}
