namespace Basket.Domain.Entities;

/// <summary>
/// One customer's basket. The aggregate root: items are only ever reached through it,
/// so quantity and price rules cannot be bypassed by editing an item directly.
/// </summary>
/// <remarks>
/// Named CustomerBasket rather than Basket because the service's root namespace is
/// already <c>Basket</c>, and a type that shadows its own namespace makes every
/// subsequent reference ambiguous.
/// </remarks>
public sealed class CustomerBasket
{
    public const int MaxQuantityPerItem = 99;

    private readonly List<BasketItem> _items = [];

    public Guid Id { get; private set; }

    /// <summary>The <c>sub</c> claim of the signed-in user. One basket per customer.</summary>
    public Guid CustomerId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public IReadOnlyList<BasketItem> Items => _items;

    /// <summary>
    /// Computed from the price snapshots held on each item, never by re-reading Catalog.
    /// </summary>
    public decimal Total => _items.Sum(item => item.UnitPrice * item.Quantity);

    // EF Core materialises through this; application code uses Create.
    private CustomerBasket()
    {
    }

    public static CustomerBasket Create(Guid customerId) => new()
    {
        Id = Guid.CreateVersion7(),
        CustomerId = customerId,
        CreatedAt = DateTimeOffset.UtcNow
    };

    /// <summary>
    /// Rebuilds a basket from stored state. <b>For persistence only.</b>
    /// </summary>
    /// <remarks>
    /// Needed because the basket is stored as a JSON document in Redis rather than mapped
    /// by an ORM: there is no change tracker to materialise entities through private
    /// setters, so the aggregate has to offer a way back in.
    ///
    /// It is a named factory rather than public setters or a public constructor so that
    /// the one legitimate caller is obvious, and so the invariants in AddItem and
    /// SetQuantity remain the only route for anything that is not replaying stored state.
    /// Replaying through Create plus AddItem would not do: it would mint a new id and
    /// overwrite CreatedAt, AddedAt and UpdatedAt with the time of the read.
    /// </remarks>
    public static CustomerBasket Rehydrate(
        Guid id,
        Guid customerId,
        DateTimeOffset createdAt,
        DateTimeOffset? updatedAt,
        IEnumerable<BasketItem> items)
    {
        var basket = new CustomerBasket
        {
            Id = id,
            CustomerId = customerId,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };

        basket._items.AddRange(items);

        return basket;
    }

    /// <summary>
    /// Adds a product, or increases the quantity if it is already present.
    /// </summary>
    /// <remarks>
    /// This is an increment, which is precisely why it is not safe to retry blindly —
    /// see the idempotency guard in AddItemHandler and ADR 0006.
    /// </remarks>
    public void AddItem(Guid productId, string productName, decimal unitPrice, int quantity, bool priceFromCache)
    {
        var existing = _items.SingleOrDefault(item => item.ProductId == productId);

        if (existing is null)
        {
            _items.Add(BasketItem.Create(Id, productId, productName, unitPrice, quantity, priceFromCache));
        }
        else
        {
            // The earlier snapshot wins. The customer is owed the price they were shown,
            // and letting a later add silently reprice the whole line is how baskets
            // start disagreeing with the order that follows them.
            existing.IncreaseQuantity(quantity);
        }

        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Returns false when the product is not in the basket.</summary>
    public bool SetQuantity(Guid productId, int quantity)
    {
        var item = _items.SingleOrDefault(candidate => candidate.ProductId == productId);

        if (item is null)
        {
            return false;
        }

        item.SetQuantity(quantity);
        UpdatedAt = DateTimeOffset.UtcNow;

        return true;
    }

    /// <summary>Returns false when the product is not in the basket.</summary>
    public bool RemoveItem(Guid productId)
    {
        var item = _items.SingleOrDefault(candidate => candidate.ProductId == productId);

        if (item is null)
        {
            return false;
        }

        _items.Remove(item);
        UpdatedAt = DateTimeOffset.UtcNow;

        return true;
    }

    public void Clear()
    {
        _items.Clear();
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
