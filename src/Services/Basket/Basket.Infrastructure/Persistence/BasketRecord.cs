using Basket.Domain.Entities;

namespace Basket.Infrastructure.Persistence;

/// <summary>
/// The stored shape of a basket: what actually becomes JSON in Redis.
/// </summary>
/// <remarks>
/// Separate from the domain entity on purpose. Serialising <see cref="CustomerBasket"/>
/// directly would make the aggregate's private fields and computed properties part of the
/// storage format, so renaming a field would silently fail to read every basket already
/// in Redis. With a record in between, the domain can change freely and this file is the
/// one place that has to keep reading old documents.
///
/// There is no Total here. It is computed from the lines, and storing a derived value is
/// how a total ends up disagreeing with the items it supposedly sums.
/// </remarks>
internal sealed record BasketRecord(
    Guid Id,
    Guid CustomerId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<BasketItemRecord> Items)
{
    public static BasketRecord From(CustomerBasket basket) => new(
        basket.Id,
        basket.CustomerId,
        basket.CreatedAt,
        basket.UpdatedAt,
        [.. basket.Items.Select(BasketItemRecord.From)]);

    public CustomerBasket ToDomain() => CustomerBasket.Rehydrate(
        Id,
        CustomerId,
        CreatedAt,
        UpdatedAt,
        Items.Select(item => item.ToDomain(Id)));
}

internal sealed record BasketItemRecord(
    Guid Id,
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    bool PriceFromCache,
    DateTimeOffset AddedAt)
{
    public static BasketItemRecord From(BasketItem item) => new(
        item.Id,
        item.ProductId,
        item.ProductName,
        item.UnitPrice,
        item.Quantity,
        item.PriceFromCache,
        item.AddedAt);

    /// <summary>
    /// The basket id is passed in rather than stored on each line: in a document store it
    /// is the key of the document the line already sits inside, so writing it on every
    /// item would be a second copy of the same fact, free to drift.
    /// </summary>
    public BasketItem ToDomain(Guid basketId) => BasketItem.Rehydrate(
        Id, basketId, ProductId, ProductName, UnitPrice, Quantity, PriceFromCache, AddedAt);
}

/// <summary>The stored shape of a fallback price.</summary>
internal sealed record PriceRecord(Guid ProductId, string Name, decimal UnitPrice, DateTimeOffset CachedAt);
