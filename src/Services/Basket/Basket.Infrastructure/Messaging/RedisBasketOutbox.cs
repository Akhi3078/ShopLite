using System.Text.Json;
using Basket.Application.Abstractions;
using Basket.Infrastructure.Persistence;
using ShopLite.Contracts;
using StackExchange.Redis;

namespace Basket.Infrastructure.Messaging;

/// <summary>
/// The hand-rolled outbox: clearing the basket and recording the event to publish happen
/// in one Redis transaction.
/// </summary>
/// <remarks>
/// Built by hand on purpose, before reaching for Wolverine's durable outbox. The library
/// version is a single configuration call, and it is impossible to appreciate what that
/// call buys — or to debug it when it misbehaves — without having written the thing it
/// replaces. ROADMAP Phase 3 says as much.
///
/// <para>
/// Note the asymmetry with ARCHITECTURE §8, which describes a SQL outbox table. The
/// principle it states is the one that matters — <i>the outbox lives in the same store as
/// the state change</i> — and Basket's store is Redis, so the outbox is a Redis list.
/// Putting Basket's outbox in SQL Server would recreate exactly the dual-write problem it
/// exists to solve, which is why "use a table" is the wrong reading of that section.
/// </para>
/// </remarks>
internal sealed class RedisBasketOutbox(IConnectionMultiplexer redis) : IBasketOutbox
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private IDatabase Database => redis.GetDatabase();

    public async Task<bool> Checkout(
        Guid customerId, BasketCheckedOut message, CancellationToken cancellationToken)
    {
        var basketKey = RedisKeys.Basket(customerId);
        var payload = JsonSerializer.Serialize(message, SerializerOptions);

        var transaction = Database.CreateTransaction();

        // The condition is what makes this safe against a concurrent checkout: if the
        // basket has gone (someone else checked out, or it expired) EXEC does not run and
        // nothing is written — rather than enqueueing an event for a basket that no
        // longer exists.
        transaction.AddCondition(Condition.KeyExists(basketKey));

        // Both writes, or neither.
        var enqueue = transaction.ListLeftPushAsync(RedisKeys.OutboxPending, payload);
        var clear = transaction.KeyDeleteAsync(basketKey);

        var committed = await transaction.ExecuteAsync();

        try
        {
            await Task.WhenAll(enqueue, clear);
        }
        catch (OperationCanceledException) when (!committed)
        {
            // Expected when the condition failed: nothing was queued for execution.
        }

        return committed;
    }
}
