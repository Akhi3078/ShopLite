using System.Text.Json;
using Basket.Application.Abstractions;
using Basket.Domain.Entities;
using StackExchange.Redis;

namespace Basket.Infrastructure.Persistence;

/// <summary>
/// Stores each basket as one JSON document in Redis, keyed by customer.
/// </summary>
/// <remarks>
/// One key per basket, not a hash per line. A basket is read and written whole — every
/// operation loads it, applies a rule, and saves it — so splitting it across fields would
/// buy partial updates nobody asks for and give up the one guarantee that matters, which
/// is that a basket is never half-written.
/// </remarks>
internal sealed class RedisBasketRepository(IConnectionMultiplexer redis) : IBasketRepository
{
    /// <summary>
    /// Web defaults: camelCase, case-insensitive on read. Pinned explicitly because the
    /// stored documents outlive the process — a future default change must not quietly
    /// make every basket already in Redis unreadable.
    /// </summary>
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private IDatabase Database => redis.GetDatabase();

    public async Task<CustomerBasket?> Get(Guid customerId, CancellationToken cancellationToken)
    {
        var stored = await Database.StringGetAsync(RedisKeys.Basket(customerId));

        if (stored.IsNullOrEmpty)
        {
            return null;
        }

        // ToString() rather than the implicit conversion: RedisValue converts to both
        // string and ReadOnlySpan<byte>, so passing it straight in is ambiguous.
        var record = JsonSerializer.Deserialize<BasketRecord>(stored.ToString(), SerializerOptions);

        return record?.ToDomain();
    }

    public async Task<bool> WasRequestApplied(string requestId, CancellationToken cancellationToken) =>
        await Database.KeyExistsAsync(RedisKeys.Request(requestId));

    public async Task<SaveOutcome> Save(
        CustomerBasket basket, string? requestId, CancellationToken cancellationToken)
    {
        var basketKey = RedisKeys.Basket(basket.CustomerId);
        var document = JsonSerializer.Serialize(BasketRecord.From(basket), SerializerOptions);

        if (requestId is not { Length: > 0 })
        {
            await Database.StringSetAsync(basketKey, document, RedisKeys.BasketLifetime);

            return SaveOutcome.Saved;
        }

        // MULTI/EXEC guarded by a server-side condition. The condition is what makes this
        // an atomic claim rather than a check followed by a write: if the request id
        // appeared since the handler's pre-check, EXEC does not run and neither write
        // lands. That is the same guarantee the SQL Server version got from a primary key,
        // obtained differently — Redis has no constraints to lean on.
        var requestKey = RedisKeys.Request(requestId);

        var transaction = Database.CreateTransaction();
        transaction.AddCondition(Condition.KeyNotExists(requestKey));

        var writeBasket = transaction.StringSetAsync(basketKey, document, RedisKeys.BasketLifetime);
        var writeMarker = transaction.StringSetAsync(requestKey, "1", RedisKeys.RequestLifetime);

        var committed = await transaction.ExecuteAsync();

        try
        {
            // These two complete only as part of EXEC. Awaiting them when it ran surfaces
            // a write error that would otherwise be swallowed; when it did not run they
            // complete as cancelled, and observing that is what keeps an unobserved task
            // from being reported later as if something had gone wrong here.
            await Task.WhenAll(writeBasket, writeMarker);
        }
        catch (OperationCanceledException) when (!committed)
        {
            // Expected: the condition failed, so nothing was queued for execution.
        }

        return committed ? SaveOutcome.Saved : SaveOutcome.DuplicateRequest;
    }
}
