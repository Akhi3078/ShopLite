using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ordering.Domain.Entities;

namespace Ordering.Infrastructure.Persistence.Configurations;

internal sealed class OrderLineConfiguration : IEntityTypeConfiguration<OrderLine>
{
    public void Configure(EntityTypeBuilder<OrderLine> builder)
    {
        builder.ToTable("OrderLines");

        builder.HasKey(line => line.Id);
        builder.Property(line => line.Id).ValueGeneratedNever();

        builder.Property(line => line.ProductName).IsRequired().HasMaxLength(200);

        // Same precision as Catalog's Price and Basket's UnitPrice. A mismatch anywhere
        // along that chain would let a rounding difference appear between what the
        // customer saw and what the order records.
        builder.Property(line => line.UnitPrice).HasPrecision(18, 2);

        // LineTotal is computed from UnitPrice and Quantity. Not stored, for the same
        // reason the basket does not store its total.
        builder.Ignore(line => line.LineTotal);

        // Deliberately NOT unique on (OrderId, ProductId). The basket enforces one line
        // per product, but an order is a historical record: if a future flow ever needs
        // the same product on two lines at different prices, the database should not be
        // the thing that forbids it.
        builder.HasIndex(line => line.OrderId);
    }
}
