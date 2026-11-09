using Basket.Application.Abstractions;
using Basket.Application.Baskets.Queries;
using Basket.Application.Common;
using Basket.Domain.Entities;
using FluentValidation;

namespace Basket.Application.Baskets.Commands;

public sealed record SetItemQuantityCommand(Guid CustomerId, Guid ProductId, int Quantity);

public sealed class SetItemQuantityValidator : AbstractValidator<SetItemQuantityCommand>
{
    public SetItemQuantityValidator()
    {
        RuleFor(command => command.CustomerId).NotEmpty();
        RuleFor(command => command.ProductId).NotEmpty();

        RuleFor(command => command.Quantity)
            .InclusiveBetween(1, CustomerBasket.MaxQuantityPerItem);
    }
}

/// <summary>
/// Sets a line to an absolute quantity.
/// </summary>
/// <remarks>
/// Worth contrasting with AddItemCommand: because this assigns rather than increments, it
/// is naturally idempotent and needs no Idempotency-Key. Applying it twice leaves the same
/// state as applying it once. Where a safe operation is available, choosing it beats
/// bolting a dedupe mechanism onto an unsafe one.
/// </remarks>
public sealed class SetItemQuantityHandler(
    IBasketRepository baskets,
    IValidator<SetItemQuantityCommand> validator)
    : ICommandHandler<SetItemQuantityCommand, BasketView>
{
    public async Task<Result<BasketView>> Handle(
        SetItemQuantityCommand command, CancellationToken cancellationToken)
    {
        if (await validator.CheckAsync(command, cancellationToken) is { } error)
        {
            return Result<BasketView>.Fail(error);
        }

        var basket = await baskets.Get(command.CustomerId, cancellationToken);

        if (basket is null || !basket.SetQuantity(command.ProductId, command.Quantity))
        {
            return Result<BasketView>.Fail(Error.NotFound(
                "Basket.ItemNotFound", $"Product {command.ProductId} is not in the basket."));
        }

        await baskets.Save(basket, requestId: null, cancellationToken);

        return Result<BasketView>.Success(BasketView.From(basket));
    }
}
