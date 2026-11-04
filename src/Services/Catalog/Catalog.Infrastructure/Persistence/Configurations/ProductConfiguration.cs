using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapping lives here, not on the entity. The Domain layer has no EF Core reference and
/// no attributes, so it stays usable without a database at all.
/// </summary>
internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(product => product.Id);

        builder.Property(product => product.Name).HasMaxLength(200).IsRequired();
        builder.Property(product => product.Description).HasMaxLength(2000).IsRequired();
        builder.Property(product => product.Category).HasMaxLength(100).IsRequired();

        // Money is always decimal with explicit precision. Left to convention, SQL Server
        // would pick decimal(18,2) here anyway, but relying on a default for money is how
        // rounding bugs get introduced later by someone who assumes differently.
        builder.Property(product => product.Price).HasPrecision(18, 2);

        builder.HasIndex(product => product.Category);
    }
}
