namespace Ordering.Domain.Entities;

/// <summary>
/// Where an order is in its life. Phase 3 only reaches <see cref="AwaitingFulfilment"/>;
/// the transitions out of it arrive with the saga in Phase 4.
/// </summary>
public enum OrderStatus
{
    /// <summary>Recorded, not yet reserved or paid for.</summary>
    AwaitingFulfilment = 0,

    Completed = 1,

    Cancelled = 2
}

/// <summary>
/// A customer's order, created from a checked-out basket.
/// </summary>
/// <remarks>
/// The prices here are the basket's snapshots, carried through the message. Ordering never
/// asks Catalog what anything costs — it could not, and should not: the order must record
/// what the customer agreed to, not what the catalog says later.
///
/// <para>
/// Note that an order exists before anything has been reserved or paid. That is not a
/// half-finished record, it is the point: the business fact "this customer ordered this"
/// is true the moment they checked out, and it must survive Inventory and Payment being
/// down. Everything after it is a separate, slower story.
/// </para>
/// </remarks>
public sealed class Order
{
    private readonly List<OrderLine> _lines = [];

    public Guid Id { get; private set; }

    public Guid CustomerId { get; private set; }

    public string CustomerEmail { get; private set; } = null!;

    public OrderStatus Status { get; private set; }

    /// <summary>
    /// The total as the basket calculated it, stored rather than recomputed.
    /// </summary>
    /// <remarks>
    /// Unlike the basket's total, this one is persisted. A basket is a working draft, so
    /// recomputing keeps it honest; an order is a record of an agreement, so it has to
    /// keep saying what was agreed even if a line is later corrected. The two choices look
    /// contradictory and are not: the difference is whether the number is a derivation or
    /// a commitment.
    /// </remarks>
    public decimal Total { get; private set; }

    public DateTimeOffset PlacedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public IReadOnlyList<OrderLine> Lines => _lines;

    private Order()
    {
    }

    public static Order Place(
        Guid customerId,
        string customerEmail,
        decimal total,
        DateTimeOffset placedAt,
        IEnumerable<(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity)> lines)
    {
        var order = new Order
        {
            Id = Guid.CreateVersion7(),
            CustomerId = customerId,
            CustomerEmail = customerEmail.Trim(),
            Status = OrderStatus.AwaitingFulfilment,
            Total = total,

            // Taken from the event, not DateTimeOffset.UtcNow. The order was placed when
            // the customer checked out, which may be measurably earlier than when this
            // message was handled — using "now" would make the record depend on how long
            // the message sat in a queue.
            PlacedAt = placedAt
        };

        order._lines.AddRange(lines.Select(line =>
            OrderLine.Create(order.Id, line.ProductId, line.ProductName, line.UnitPrice, line.Quantity)));

        return order;
    }

    /// <summary>Rebuilds an order from stored state. For persistence only.</summary>
    public static Order Rehydrate(
        Guid id,
        Guid customerId,
        string customerEmail,
        OrderStatus status,
        decimal total,
        DateTimeOffset placedAt,
        DateTimeOffset? updatedAt,
        IEnumerable<OrderLine> lines)
    {
        var order = new Order
        {
            Id = id,
            CustomerId = customerId,
            CustomerEmail = customerEmail,
            Status = status,
            Total = total,
            PlacedAt = placedAt,
            UpdatedAt = updatedAt
        };

        order._lines.AddRange(lines);

        return order;
    }

    public void Complete()
    {
        Status = OrderStatus.Completed;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Cancel()
    {
        Status = OrderStatus.Cancelled;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
