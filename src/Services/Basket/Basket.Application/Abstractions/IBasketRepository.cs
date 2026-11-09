using Basket.Domain.Entities;

namespace Basket.Application.Abstractions;

/// <summary>
/// Basket's exclusive view of its own store. No other service reaches it.
/// </summary>
/// <remarks>
/// A repository rather than the <c>ICatalogDbContext</c>-style port Catalog uses. Catalog's
/// store is relational, so exposing <c>DbSet</c> lets its handlers write queries, which
/// is the point of that compromise ([ADR 0004]). Basket's store is a key-value document
/// store: there is nothing to query, only whole baskets to load and save. Handing the
/// Application layer an <c>IDatabase</c> would leak Redis into it for no gain.
///
/// The interface says nothing about Redis, JSON, keys or expiry. Those are decisions
/// <c>RedisBasketRepository</c> owns, which is what makes the store swappable.
/// </remarks>
public interface IBasketRepository
{
    Task<CustomerBasket?> Get(Guid customerId, CancellationToken cancellationToken);

    /// <summary>
    /// True when a request with this id has already been applied.
    /// </summary>
    /// <remarks>
    /// The cheap pre-check for the common case: a sequential retry. It cannot be the only
    /// check, because two concurrent retries can both pass it — that race is closed by
    /// <see cref="Save"/> returning <see cref="SaveOutcome.DuplicateRequest"/>.
    /// </remarks>
    Task<bool> WasRequestApplied(string requestId, CancellationToken cancellationToken);

    /// <summary>
    /// Saves the basket and, when <paramref name="requestId"/> is supplied, records that
    /// the request was applied — the two becoming visible together or not at all.
    /// </summary>
    /// <remarks>
    /// The request id is a parameter rather than a separate <c>MarkRequestApplied</c> call
    /// because the atomicity is the whole point. Two calls would leave a window in which a
    /// crash makes an applied operation replayable, or marks an operation applied that was
    /// not. Where that boundary falls is a property of the store, so the port expresses it
    /// instead of hoping callers remember the ordering.
    /// </remarks>
    Task<SaveOutcome> Save(CustomerBasket basket, string? requestId, CancellationToken cancellationToken);
}

public enum SaveOutcome
{
    Saved,

    /// <summary>
    /// The request id was already recorded, so nothing was written. The work this request
    /// describes has already been done by whoever got there first.
    /// </summary>
    DuplicateRequest
}
