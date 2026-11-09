namespace ShopLite.Contracts;

/// <summary>
/// A customer has checked out their basket. Published by Basket, consumed by Ordering.
/// </summary>
/// <remarks>
/// An <b>event</b>, not a command, and the distinction is in the name: it states a fact
/// in the past tense. Basket is not asking anyone to create an order — it does not know
/// Ordering exists. Anyone who cares may subscribe, and Basket does not change when they
/// do. A command would be <c>CreateOrder</c>, sent to exactly one known handler.
///
/// <para>
/// The event carries <b>everything Ordering needs</b> — names and prices included, not
/// just product ids. If it carried only ids, Ordering would have to call Basket or
/// Catalog back to make sense of it, which reintroduces exactly the synchronous coupling
/// that moving to messaging was meant to remove. The cost is a fatter message and
/// duplicated data; that is the trade, and it is the right way round.
/// </para>
///
/// <para>
/// Prices are the snapshots Basket took when each item was added, so the order is created
/// with the prices the customer actually saw, even if Catalog has changed them since.
/// </para>
/// </remarks>
/// <param name="MessageId">
/// Identifies this specific publication, so a consumer can recognise a redelivery.
/// Service Bus guarantees <i>at-least-once</i> delivery, never exactly-once, so every
/// consumer will eventually see the same message twice and must make the second one a
/// no-op. See <c>ProcessedMessage</c> in Ordering.
/// </param>
/// <param name="CheckedOutAt">
/// When the checkout happened, as decided by Basket. Not when the message was received —
/// those differ by however long the message waited, and using receive time would make the
/// order's history depend on how busy the queue was.
/// </param>
public sealed record BasketCheckedOut(
    Guid MessageId,
    Guid BasketId,
    Guid CustomerId,
    string CustomerEmail,
    decimal Total,
    IReadOnlyList<BasketCheckedOutLine> Lines,
    DateTimeOffset CheckedOutAt);

/// <param name="UnitPrice">The snapshot Basket held, not a price read at checkout time.</param>
public sealed record BasketCheckedOutLine(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity);
