namespace Ordering.Domain.Entities;

/// <summary>
/// A record that one message has already been handled.
/// </summary>
/// <remarks>
/// This is the answer to at-least-once delivery, and it is not optional.
///
/// <para>
/// Service Bus — like every broker worth using — guarantees a message arrives <i>at least
/// once</i>, never exactly once. Exactly-once delivery is not available: the broker cannot
/// know whether a consumer that stopped responding processed the message before it died or
/// not, so its only safe choice is to deliver again. A redelivery therefore is not an edge
/// case to tolerate, it is a certainty to design for.
/// </para>
///
/// <para>
/// Creating an order is not naturally idempotent — handling the same
/// <c>BasketCheckedOut</c> twice would give the customer two identical orders and two
/// charges. The primary key on <see cref="MessageId"/> makes the second attempt collide
/// instead, and it commits in the same transaction as the order so there is no window in
/// which one exists without the other.
/// </para>
///
/// <para>
/// Compare with Basket's <c>idem:</c> keys: same idea, different layer. There the
/// duplicate came from an HTTP retry and the key was supplied by the client; here it comes
/// from the broker and the id travels with the message.
/// </para>
/// </remarks>
public sealed class ProcessedMessage
{
    /// <summary>The publisher's message id. Primary key, so replays collide by design.</summary>
    public Guid MessageId { get; private set; }

    public string MessageType { get; private set; } = null!;

    public DateTimeOffset HandledAt { get; private set; }

    private ProcessedMessage()
    {
    }

    public static ProcessedMessage Create(Guid messageId, string messageType) => new()
    {
        MessageId = messageId,
        MessageType = messageType,
        HandledAt = DateTimeOffset.UtcNow
    };
}
