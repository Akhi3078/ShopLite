using Basket.Application.Abstractions;
using Basket.Application.Common;
using Basket.Domain.Entities;

namespace Basket.Application.Baskets.Queries;

public sealed record GetBasketQuery(Guid CustomerId);

/// <summary>
/// The read shape of a basket. Separate from the entity so the HTTP contract does not
/// move every time the aggregate does.
/// </summary>
/// <param name="ContainsCachedPrices">
/// True when any line was priced from the fallback cache rather than from Catalog. The
/// caller can then warn the customer instead of presenting a stale total as authoritative.
/// </param>
public sealed record BasketView(
    Guid Id,
    Guid CustomerId,
    IReadOnlyList<BasketItemView> Items,
    decimal Total,
    bool ContainsCachedPrices)
{
    public static BasketView From(CustomerBasket basket) => new(
        basket.Id,
        basket.CustomerId,
        [.. basket.Items
            .OrderBy(item => item.AddedAt)
            .Select(item => new BasketItemView(
                item.ProductId,
                item.ProductName,
                item.UnitPrice,
                item.Quantity,
                item.UnitPrice * item.Quantity,
                item.PriceFromCache))],
        basket.Total,
        basket.Items.Any(item => item.PriceFromCache));

    /// <summary>
    /// A customer who has never added anything has an empty basket, not a missing one.
    /// Returning 404 here would make every client special-case its own first request.
    /// </summary>
    public static BasketView Empty(Guid customerId) => new(Guid.Empty, customerId, [], 0m, false);
}

public sealed record BasketItemView(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal,
    bool PriceFromCache);

public sealed class GetBasketHandler(IBasketRepository baskets)
    : IQueryHandler<GetBasketQuery, BasketView>
{
    public async Task<Result<BasketView>> Handle(
        GetBasketQuery query, CancellationToken cancellationToken)
    {
        var basket = await baskets.Get(query.CustomerId, cancellationToken);

        return Result<BasketView>.Success(
            basket is null ? BasketView.Empty(query.CustomerId) : BasketView.From(basket));
    }
}
