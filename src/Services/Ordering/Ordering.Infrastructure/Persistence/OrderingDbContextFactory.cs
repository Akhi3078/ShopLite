using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ordering.Infrastructure.Persistence;

/// <summary>
/// Design-time only, as in Catalog. <c>dotnet ef</c> runs outside the AppHost, so no
/// connection string is injected and <c>AddSqlServerDbContext</c> never executes. This is
/// also what lets Infrastructure act as its own startup project, keeping EF Core out of
/// Ordering.Api.
/// </summary>
/// <remarks>
/// Set <c>ConnectionStrings__orderingdb</c> to target a different server.
/// </remarks>
internal sealed class OrderingDbContextFactory : IDesignTimeDbContextFactory<OrderingDbContext>
{
    private const string LocalDefault =
        "Server=localhost;Database=ShopLite_Ordering;Trusted_Connection=True;TrustServerCertificate=True";

    public OrderingDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__orderingdb") ?? LocalDefault;

        var options = new DbContextOptionsBuilder<OrderingDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new OrderingDbContext(options);
    }
}
