using Basket.Application.Abstractions;
using Basket.Application.Common;

namespace Basket.Application.Baskets.Commands;

public sealed record ClearBasketCommand(Guid CustomerId);

/// <summary>
/// Empties the basket. Succeeds whether or not there was anything in it: "make it empty"
/// is a goal state, so there is no sensible way for it to fail twice.
/// </summary>
/// <remarks>
/// Saves an emptied basket rather than deleting the key, so that a cleared basket and a
/// basket whose last line was removed end up in exactly the same state. Deleting here
/// would make those two paths differ for no reason a caller could observe or predict.
/// Either way the record expires on its own; see RedisBasketRepository.
/// </remarks>
public sealed class ClearBasketHandler(IBasketRepository baskets) : ICommandHandler<ClearBasketCommand>
{
    public async Task<Result> Handle(ClearBasketCommand command, CancellationToken cancellationToken)
    {
        var basket = await baskets.Get(command.CustomerId, cancellationToken);

        if (basket is null)
        {
            return Result.Success();
        }

        basket.Clear();
        await baskets.Save(basket, requestId: null, cancellationToken);

        return Result.Success();
    }
}
