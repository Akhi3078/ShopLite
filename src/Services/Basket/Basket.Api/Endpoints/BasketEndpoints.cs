using Basket.Api.Extensions;
using Basket.Application.Abstractions;
using Basket.Application.Baskets.Commands;
using Basket.Application.Baskets.Queries;
using Microsoft.AspNetCore.Mvc;
using ShopLite.ServiceDefaults;

namespace Basket.Api.Endpoints;

/// <summary>
/// HTTP surface for the signed-in customer's basket.
/// </summary>
/// <remarks>
/// Every route is implicitly scoped to the caller: the customer id comes from the token
/// via <see cref="ICallerContext"/>, never from the URL or the body. There is deliberately
/// no /baskets/{customerId} route, because then the service would have to decide whether
/// the caller is allowed to read that basket, and that check is easy to forget.
/// </remarks>
public static class BasketEndpoints
{
    public static IEndpointRouteBuilder MapBasket(this IEndpointRouteBuilder app)
    {
        var basket = app.MapGroup("/basket")
            .WithTags("Basket")
            .RequireAuthorization(Policies.Authenticated);

        basket.MapGet("/", async (
                ICallerContext caller,
                IQueryHandler<GetBasketQuery, BasketView> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new GetBasketQuery(caller.CustomerId), cancellationToken);
                return result.ToHttpResult();
            })
            .WithName("GetBasket")
            .WithSummary("Get the current customer's basket")
            .WithDescription(
                "Returns an empty basket rather than 404 when the customer has never added anything.");

        basket.MapPost("/items", async (
                AddItemRequest request,
                ICallerContext caller,
                ICommandHandler<AddItemCommand, BasketView> handler,
                CancellationToken cancellationToken,
                [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey = null) =>
            {
                var command = new AddItemCommand(
                    caller.CustomerId, request.ProductId, request.Quantity, idempotencyKey);

                var result = await handler.Handle(command, cancellationToken);
                return result.ToHttpResult();
            })
            .WithName("AddBasketItem")
            .WithSummary("Add a product to the basket")
            .WithDescription(
                "Prices the product through Catalog. Adding the same product again increases "
                + "the quantity and keeps the original price snapshot. Send an Idempotency-Key "
                + "header to make a retry of this call safe; without one, a retry adds the "
                + "quantity twice. Returns 503 if Catalog is unreachable and no price is cached.");

        basket.MapPut("/items/{productId:guid}", async (
                Guid productId,
                SetQuantityRequest request,
                ICallerContext caller,
                ICommandHandler<SetItemQuantityCommand, BasketView> handler,
                CancellationToken cancellationToken) =>
            {
                var command = new SetItemQuantityCommand(caller.CustomerId, productId, request.Quantity);

                var result = await handler.Handle(command, cancellationToken);
                return result.ToHttpResult();
            })
            .WithName("SetBasketItemQuantity")
            .WithSummary("Set a line to an absolute quantity")
            .WithDescription(
                "Naturally idempotent, so no Idempotency-Key is needed. Does not call Catalog: "
                + "the price snapshot taken when the item was added is kept.");

        basket.MapDelete("/items/{productId:guid}", async (
                Guid productId,
                ICallerContext caller,
                ICommandHandler<RemoveItemCommand, BasketView> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(
                    new RemoveItemCommand(caller.CustomerId, productId), cancellationToken);

                return result.ToHttpResult();
            })
            .WithName("RemoveBasketItem")
            .WithSummary("Remove one line from the basket");

        basket.MapPost("/checkout", async (
                ICallerContext caller,
                ICommandHandler<CheckoutCommand, CheckoutResult> handler,
                CancellationToken cancellationToken) =>
            {
                var command = new CheckoutCommand(caller.CustomerId, caller.Email);

                var result = await handler.Handle(command, cancellationToken);

                // 202 Accepted, not 200 or 201 — and the status code is the lesson.
                //
                // The basket is emptied and the event is recorded, but no order exists
                // yet: Ordering has not necessarily even received the message. Returning
                // 201 Created would be a lie about a resource the caller cannot fetch,
                // and 200 would imply the work is finished. 202 says "accepted, not yet
                // done", which is exactly what eventual consistency feels like at the
                // edge of an asynchronous system.
                return result.IsSuccess
                    ? Results.Accepted(value: result.Value)
                    : result.ToHttpResult();
            })
            .WithName("Checkout")
            .WithSummary("Check out the basket")
            .WithDescription(
                "Empties the basket and publishes BasketCheckedOut. Returns 202: the order "
                + "is created asynchronously by Ordering, so it will not exist the instant "
                + "this returns. Poll GET /orders for it.");

        basket.MapDelete("/", async (
                ICallerContext caller,
                ICommandHandler<ClearBasketCommand> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(
                    new ClearBasketCommand(caller.CustomerId), cancellationToken);

                return result.ToHttpResult();
            })
            .WithName("ClearBasket")
            .WithSummary("Empty the basket");

        return app;
    }

    /// <summary>
    /// Api-specific shapes. The customer id is absent on purpose: it comes from the token,
    /// so accepting it in the body would create a field that must be ignored, and an
    /// ignored field in a request body is an invitation to assume it works.
    /// </summary>
    private sealed record AddItemRequest(Guid ProductId, int Quantity);

    private sealed record SetQuantityRequest(int Quantity);
}
