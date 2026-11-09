using Basket.Application.Abstractions;
using Basket.Application.Baskets.Queries;
using Basket.Application.Common;
using Basket.Domain.Entities;
using FluentValidation;

namespace Basket.Application.Baskets.Commands;

/// <param name="RequestId">
/// The caller's Idempotency-Key, if it supplied one. Optional, because requiring it would
/// break any client that has not been taught about it yet; but without it a retry of this
/// command adds the quantity a second time.
/// </param>
public sealed record AddItemCommand(Guid CustomerId, Guid ProductId, int Quantity, string? RequestId);

public sealed class AddItemValidator : AbstractValidator<AddItemCommand>
{
    public AddItemValidator()
    {
        RuleFor(command => command.CustomerId).NotEmpty();
        RuleFor(command => command.ProductId).NotEmpty();

        RuleFor(command => command.Quantity)
            .InclusiveBetween(1, CustomerBasket.MaxQuantityPerItem);

        RuleFor(command => command.RequestId)
            .MaximumLength(128)
            .When(command => command.RequestId is not null);
    }
}

/// <summary>
/// Adds a product to the caller's basket, pricing it through Catalog.
/// </summary>
/// <remarks>
/// The one handler in ShopLite that depends on another service, so also the one that has
/// to answer the awkward questions: what if Catalog is slow, what if it is down, and what
/// if this very request arrives twice. The first two are answered by
/// <see cref="ICatalogPriceReader"/>; the third is answered here.
/// </remarks>
public sealed class AddItemHandler(
    IBasketRepository baskets,
    ICatalogPriceReader catalog,
    IValidator<AddItemCommand> validator)
    : ICommandHandler<AddItemCommand, BasketView>
{
    public async Task<Result<BasketView>> Handle(
        AddItemCommand command, CancellationToken cancellationToken)
    {
        if (await validator.CheckAsync(command, cancellationToken) is { } error)
        {
            return Result<BasketView>.Fail(error);
        }

        // Idempotency check, before anything is changed. Adding an item is an increment,
        // so replaying it is not harmless: the customer ends up with two of something
        // they asked for once. A retry is not hypothetical either — a resilience
        // pipeline, an impatient click, or a gateway timeout all produce one.
        if (command.RequestId is { Length: > 0 } requestId
            && await baskets.WasRequestApplied(requestId, cancellationToken))
        {
            return await CurrentBasket(command.CustomerId, cancellationToken);
        }

        var price = await catalog.GetPrice(command.ProductId, cancellationToken);

        if (!price.IsSuccess)
        {
            // NotFound (no such product) and Unavailable (Catalog unreachable, nothing
            // cached) arrive here already distinguished, so the Api layer can map them to
            // 404 and 503 without knowing why either happened.
            return Result<BasketView>.Fail(price.Error!);
        }

        var basket = await baskets.Get(command.CustomerId, cancellationToken)
                     ?? CustomerBasket.Create(command.CustomerId);

        var product = price.Value!;
        basket.AddItem(product.ProductId, product.Name, product.UnitPrice, command.Quantity, product.FromCache);

        var outcome = await baskets.Save(basket, command.RequestId, cancellationToken);

        if (outcome is SaveOutcome.DuplicateRequest)
        {
            // An identical request committed between the pre-check and this save. The
            // work is already done, so it is reported rather than retried: the sequential
            // replay path returns the basket above, and only a genuine race lands here.
            return Result<BasketView>.Fail(Error.Conflict(
                "Basket.DuplicateRequest",
                "A request with this Idempotency-Key has already been applied."));
        }

        return Result<BasketView>.Success(BasketView.From(basket));
    }

    private async Task<Result<BasketView>> CurrentBasket(Guid customerId, CancellationToken cancellationToken)
    {
        var basket = await baskets.Get(customerId, cancellationToken);

        return Result<BasketView>.Success(
            basket is null ? BasketView.Empty(customerId) : BasketView.From(basket));
    }
}
