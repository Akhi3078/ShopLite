using ShopLite.Contracts;

namespace Basket.Application.Abstractions;

/// <summary>
/// Checkout: empties the basket and records the event to publish, together.
/// </summary>
/// <remarks>
/// This exists because of the <b>dual-write problem</b>, which is the single most
/// important idea in Phase 3.
///
/// <para>
/// Checkout has to do two things: change state (the basket is done with) and tell the
/// world (<c>BasketCheckedOut</c>). Those live in two different systems — Redis and
/// Service Bus — so there is no transaction that covers both. Whichever order you choose,
/// a crash in the middle is wrong:
/// </para>
///
/// <list type="bullet">
/// <item>Publish first, then save: a crash leaves an order being created for a basket
/// that was never checked out.</item>
/// <item>Save first, then publish: a crash leaves a customer whose basket emptied and
/// whose order never appears. Worse, because it is silent.</item>
/// </list>
///
/// <para>
/// No amount of careful ordering fixes it. The outbox does: write the event <i>into the
/// same store as the state change</i>, in one atomic operation, and let a background
/// publisher send it afterwards. Then there is only one write, so it either happened or it
/// did not — and the publisher can retry for as long as it takes.
/// </para>
///
/// <para>
/// The cost is that publishing is now <i>at-least-once</i>: the publisher can send a
/// message and die before recording that it did, so it sends again. That is why every
/// consumer needs to be idempotent, and why <c>BasketCheckedOut</c> carries a message id.
/// The outbox does not remove the problem, it moves it somewhere it can be solved.
/// </para>
/// </remarks>
public interface IBasketOutbox
{
    /// <summary>
    /// Atomically clears the basket and enqueues the event. Returns false when there was
    /// nothing to check out.
    /// </summary>
    Task<bool> Checkout(Guid customerId, BasketCheckedOut message, CancellationToken cancellationToken);
}
