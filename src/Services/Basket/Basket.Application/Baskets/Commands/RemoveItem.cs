using Basket.Application.Abstractions;
using Basket.Application.Baskets.Queries;
using Basket.Application.Common;

namespace Basket.Application.Baskets.Commands;

public sealed record RemoveItemCommand(Guid CustomerId, Guid ProductId);

/// <summary>
/// Removes one line. Reports a missing line as NotFound, so a client that deletes twice
/// learns that the second call did nothing.
/// </summary>
public sealed class RemoveItemHandler(IBasketRepository baskets)
    : ICommandHandler<RemoveItemCommand, BasketView>
{
    public async Task<Result<BasketView>> Handle(
        RemoveItemCommand command, CancellationToken cancellationToken)
    {
        var basket = await baskets.Get(command.CustomerId, cancellationToken);

        if (basket is null || !basket.RemoveItem(command.ProductId))
        {
            return Result<BasketView>.Fail(Error.NotFound(
                "Basket.ItemNotFound", $"Product {command.ProductId} is not in the basket."));
        }

        await baskets.Save(basket, requestId: null, cancellationToken);

        return Result<BasketView>.Success(BasketView.From(basket));
    }
}
