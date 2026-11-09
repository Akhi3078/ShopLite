using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Ordering.Domain.Entities;

namespace Ordering.Application.Abstractions;

/// <summary>
/// Ordering's exclusive view of its own database (ShopLite_Ordering).
/// </summary>
public interface IOrderingDbContext
{
    DbSet<Order> Orders { get; }

    /// <summary>
    /// The dedupe table for at-least-once delivery. On the port because the use case that
    /// handles a message is the thing that has to check it — hiding it in Infrastructure
    /// would mean the idempotency decision happened somewhere nobody reading the handler
    /// could see.
    /// </summary>
    DbSet<ProcessedMessage> ProcessedMessages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts an explicit transaction.
    /// </summary>
    /// <remarks>
    /// Exposed because the outbox pattern needs it: a state change and the record of its
    /// event must commit together or not at all, and a single <c>SaveChanges</c> is only
    /// enough while everything involved is tracked by the same context. Phase 4 adds
    /// writes that are not, which is when this starts earning its keep.
    /// </remarks>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
