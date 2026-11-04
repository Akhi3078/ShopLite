using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Identity.Api.Persistence;

/// <summary>Design-time context for `dotnet ef`, which runs outside the AppHost.</summary>
internal sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<ShopLiteIdentityDbContext>
{
    private const string LocalDefault =
        "Server=localhost;Database=ShopLite_Identity;Trusted_Connection=True;TrustServerCertificate=True";

    public ShopLiteIdentityDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__identitydb") ?? LocalDefault;

        var options = new DbContextOptionsBuilder<ShopLiteIdentityDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new ShopLiteIdentityDbContext(options);
    }
}
