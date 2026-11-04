using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Catalog.Infrastructure.Persistence;

/// <summary>
/// Development-only sample data, so there is something to query on first run.
/// </summary>
public static class SeedData
{
    public static async Task SeedCatalogAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        // Migrations are applied explicitly with `dotnet ef database update`, not here.
        // Automatic migration on startup is convenient and is exactly the habit that
        // makes zero-downtime deployment hard later (ROADMAP Phase 8).
        if (await db.Products.AnyAsync())
        {
            return;
        }

        db.Products.AddRange(
            Product.Create("Aeron Chair", "Ergonomic office chair with lumbar support.", 1189.00m, "Furniture"),
            Product.Create("Mechanical Keyboard", "Tenkeyless board with tactile switches.", 149.99m, "Peripherals"),
            Product.Create("27\" 4K Monitor", "IPS panel, 60Hz, USB-C power delivery.", 429.50m, "Displays"),
            Product.Create("Noise Cancelling Headphones", "Over-ear, 30 hour battery.", 279.00m, "Audio"),
            Product.Create("Standing Desk", "Electric height adjustment, 160x80cm.", 699.00m, "Furniture"));

        await db.SaveChangesAsync();
    }
}
