using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Ordering.Application.Abstractions;
using Ordering.Domain.Entities;

namespace Ordering.Infrastructure.Persistence;

/// <summary>
/// Ordering's exclusive view of its own database (ShopLite_Ordering).
/// </summary>
public sealed class OrderingDbContext(DbContextOptions<OrderingDbContext> options)
    : DbContext(options), IOrderingDbContext
{
    public DbSet<Order> Orders => Set<Order>();

    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        Database.BeginTransactionAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderingDbContext).Assembly);
}
