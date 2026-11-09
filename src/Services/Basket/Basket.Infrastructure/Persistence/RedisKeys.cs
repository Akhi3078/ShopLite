namespace Basket.Infrastructure.Persistence;

/// <summary>
/// Every Redis key this service owns, in one place.
/// </summary>
/// <remarks>
/// Redis has no schema, so the key layout <i>is</i> the schema — and unlike a database
/// there is nothing to stop two parts of the code inventing slightly different spellings
/// of the same key. Keeping them here means the layout can be read at a glance, and that
/// a stray `"basket:" + id` somewhere else stands out as a mistake.
///
/// The prefixes double as a namespace. Nothing else shares this Redis instance today, but
/// Phase 3 may add more, and `basket:` is what keeps a careless `KEYS *` honest.
/// </remarks>
internal static class RedisKeys
{
    /// <summary>One basket per customer, keyed by the <c>sub</c> claim.</summary>
    public static string Basket(Guid customerId) => $"basket:{customerId}";

    /// <summary>Last price seen from Catalog, used only as a fallback.</summary>
    public static string Price(Guid productId) => $"price:{productId}";

    /// <summary>A client-supplied Idempotency-Key that has been applied.</summary>
    public static string Request(string requestId) => $"idem:{requestId}";

    /// <summary>
    /// Events written at checkout, waiting to be published.
    /// </summary>
    /// <remarks>
    /// No TTL, unlike everything else here. An unpublished event is not stale data that
    /// may be dropped — it is an order that has not happened yet. Expiring it would lose
    /// a customer's purchase silently, which is the one outcome the outbox exists to
    /// prevent.
    /// </remarks>
    public const string OutboxPending = "outbox:pending";

    /// <summary>
    /// Events claimed by the publisher but not yet confirmed as sent.
    /// </summary>
    /// <remarks>
    /// The crash window made visible. Anything sitting here when the process restarts may
    /// or may not have reached the broker, so it is republished — which is precisely why
    /// consumers must be idempotent.
    /// </remarks>
    public const string OutboxInFlight = "outbox:inflight";

    /// <summary>
    /// How long an untouched basket survives. Long enough that a customer can come back
    /// tomorrow, short enough that abandoned baskets do not accumulate forever.
    /// </summary>
    /// <remarks>
    /// Refreshed on every write, so it behaves as a sliding expiry. This is the TTL the
    /// original Redis decision in ARCHITECTURE.md §12 was about: under SQL Server it would
    /// have needed a scheduled sweep, and here it is one argument to a set.
    /// </remarks>
    public static readonly TimeSpan BasketLifetime = TimeSpan.FromDays(30);

    /// <summary>
    /// How long a fallback price stays usable. Bounds how stale a cached price can be,
    /// which the SQL Server version of this cache did not do at all.
    /// </summary>
    public static readonly TimeSpan PriceLifetime = TimeSpan.FromHours(24);

    /// <summary>
    /// How long a request id is remembered. It only has to outlive the window in which a
    /// retry is plausible; keeping it forever would be a slow leak with no benefit.
    /// </summary>
    public static readonly TimeSpan RequestLifetime = TimeSpan.FromHours(24);
}
