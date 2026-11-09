using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ordering.Domain.Entities;

namespace Ordering.Infrastructure.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");

        builder.HasKey(order => order.Id);

        // Application-assigned Guid v7 keys, so the store must not try to generate them.
        // Omitting this is what produced UPDATE-instead-of-INSERT in Basket; see the same
        // line in BasketItemConfiguration and ADR 0006.
        builder.Property(order => order.Id).ValueGeneratedNever();

        builder.Property(order => order.CustomerEmail).IsRequired().HasMaxLength(256);

        builder.Property(order => order.Total).HasPrecision(18, 2);

        // Stored as a string, not an int. An int in the database means a migration or a
        // lookup table every time the enum changes order, and makes the raw rows
        // unreadable to anyone querying them by hand.
        builder.Property(order => order.Status)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.HasMany(order => order.Lines)
            .WithOne()
            .HasForeignKey(line => line.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(Order.Lines))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // The two queries that exist: a customer's own orders, newest first.
        builder.HasIndex(order => new { order.CustomerId, order.PlacedAt });
    }
}
