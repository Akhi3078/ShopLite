using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Catalog.Infrastructure.Persistence;

/// <summary>
/// Design-time only. `dotnet ef` runs outside the AppHost, so no connection string is
/// injected and <c>AddSqlServerDbContext</c> never executes. Without this, the tooling
/// would try to boot the Api project — including its seeder — just to read the model.
/// </summary>
/// <remarks>
/// Set <c>ConnectionStrings__catalogdb</c> to target a different server, e.g. when
/// generating or applying migrations against Azure SQL in Phase 7.
/// </remarks>
internal sealed class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    private const string LocalDefault =
        "Server=localhost;Database=ShopLite_Catalog;Trusted_Connection=True;TrustServerCertificate=True";

    public CatalogDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__catalogdb") ?? LocalDefault;

        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new CatalogDbContext(options);
    }
}
