using Catalog.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Infrastructure;

/// <summary>
/// Catalog's exclusive view of its own database (ShopLite_Catalog).
/// No other service holds a connection string for it.
/// </summary>
public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var product = modelBuilder.Entity<Product>();

        product.ToTable("Products");
        product.HasKey(p => p.Id);

        product.Property(p => p.Name).HasMaxLength(200).IsRequired();
        product.Property(p => p.Description).HasMaxLength(2000).IsRequired();
        product.Property(p => p.Category).HasMaxLength(100).IsRequired();

        // Money is always decimal with explicit precision. Left to convention, SQL Server
        // would pick decimal(18,2) here anyway, but relying on a default for money is how
        // rounding bugs get introduced later by someone who assumes differently.
        product.Property(p => p.Price).HasPrecision(18, 2);

        product.HasIndex(p => p.Category);
    }
}
