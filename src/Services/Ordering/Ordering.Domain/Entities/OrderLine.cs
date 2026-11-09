namespace Ordering.Domain.Entities;

/// <summary>
/// One line of an order. Immutable once placed — correcting an order means a new
/// transaction against it, not editing history.
/// </summary>
public sealed class OrderLine
{
    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public Guid ProductId { get; private set; }

    /// <summary>
    /// Copied from the event, so the order reads correctly years later even if Catalog
    /// has renamed or deleted the product.
    /// </summary>
    public string ProductName { get; private set; } = null!;

    public decimal UnitPrice { get; private set; }

    public int Quantity { get; private set; }

    public decimal LineTotal => UnitPrice * Quantity;

    private OrderLine()
    {
    }

    internal static OrderLine Create(
        Guid orderId, Guid productId, string productName, decimal unitPrice, int quantity) => new()
        {
            Id = Guid.CreateVersion7(),
            OrderId = orderId,
            ProductId = productId,
            ProductName = productName.Trim(),
            UnitPrice = unitPrice,
            Quantity = quantity
        };
}
