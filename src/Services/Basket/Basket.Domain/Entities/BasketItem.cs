namespace Basket.Domain.Entities;

/// <summary>
/// A line in a basket, carrying a <b>price snapshot</b> rather than a live price.
/// </summary>
/// <remarks>
/// The price is copied in at the moment the item is added and never refreshed. Checkout
/// therefore does not call Catalog again: the price the customer saw is the price that
/// becomes the order. This is also what makes a Catalog outage survivable — see
/// ARCHITECTURE.md §8 and the fallback in CatalogPriceReader.
/// </remarks>
public sealed class BasketItem
{
    public Guid Id { get; private set; }

    public Guid BasketId { get; private set; }

    public Guid ProductId { get; private set; }

    /// <summary>Denormalised from Catalog so the basket renders without a second call.</summary>
    public string ProductName { get; private set; } = null!;

    public decimal UnitPrice { get; private set; }

    public int Quantity { get; private set; }

    /// <summary>
    /// True when the price came from Basket's fallback cache because Catalog was
    /// unreachable. Surfaced to the caller so degraded data is visible, not silent.
    /// </summary>
    public bool PriceFromCache { get; private set; }

    public DateTimeOffset AddedAt { get; private set; }

    private BasketItem()
    {
    }

    internal static BasketItem Create(
        Guid basketId, Guid productId, string productName, decimal unitPrice, int quantity, bool priceFromCache) => new()
        {
            Id = Guid.CreateVersion7(),
            BasketId = basketId,
            ProductId = productId,
            ProductName = productName.Trim(),
            UnitPrice = unitPrice,
            Quantity = Math.Clamp(quantity, 1, CustomerBasket.MaxQuantityPerItem),
            PriceFromCache = priceFromCache,
            AddedAt = DateTimeOffset.UtcNow
        };

    /// <summary>
    /// Rebuilds a line from stored state. <b>For persistence only</b> — see
    /// <see cref="CustomerBasket.Rehydrate"/>.
    /// </summary>
    public static BasketItem Rehydrate(
        Guid id,
        Guid basketId,
        Guid productId,
        string productName,
        decimal unitPrice,
        int quantity,
        bool priceFromCache,
        DateTimeOffset addedAt) => new()
        {
            Id = id,
            BasketId = basketId,
            ProductId = productId,
            ProductName = productName,
            UnitPrice = unitPrice,

            // Not clamped, unlike Create. Stored state is replayed as it is; silently
            // "fixing" it here would hide a bad write instead of surfacing it.
            Quantity = quantity,
            PriceFromCache = priceFromCache,
            AddedAt = addedAt
        };

    internal void IncreaseQuantity(int by) =>
        Quantity = Math.Clamp(Quantity + by, 1, CustomerBasket.MaxQuantityPerItem);

    internal void SetQuantity(int quantity) =>
        Quantity = Math.Clamp(quantity, 1, CustomerBasket.MaxQuantityPerItem);
}
