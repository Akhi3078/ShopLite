using Basket.Application.Abstractions;
using Basket.Application.Common;
using ShopLite.Contracts;

namespace Basket.Application.Baskets.Commands;

public sealed record CheckoutCommand(Guid CustomerId, string CustomerEmail);

public sealed record CheckoutResult(Guid MessageId, decimal Total, int LineCount);

/// <summary>
/// Checks out the caller's basket: empties it and publishes <see cref="BasketCheckedOut"/>.
/// </summary>
/// <remarks>
/// Notice what this handler does <b>not</b> do: it does not create an order, and it does
/// not call Ordering. It states that a checkout happened and stops. Whether that produces
/// an order, an email, a loyalty-points adjustment or nothing at all is not Basket's
/// business — and that is what makes it an event rather than a command.
///
/// <para>
/// The consequence is eventual consistency, and it is visible to the customer: for a
/// moment after checkout the basket is empty and no order exists yet. The UI says so
/// rather than pretending otherwise.
/// </para>
/// </remarks>
public sealed class CheckoutHandler(IBasketRepository baskets, IBasketOutbox outbox)
    : ICommandHandler<CheckoutCommand, CheckoutResult>
{
    public async Task<Result<CheckoutResult>> Handle(
        CheckoutCommand command, CancellationToken cancellationToken)
    {
        var basket = await baskets.Get(command.CustomerId, cancellationToken);

        if (basket is null || basket.Items.Count == 0)
        {
            return Result<CheckoutResult>.Fail(Error.Validation(
                new Dictionary<string, string[]>
                {
                    ["Basket"] = ["There is nothing to check out."]
                }));
        }

        // The id is minted here, by the publisher, and travels with the message. It has to
        // be stable across republishes: if the outbox publisher sends this twice, both
        // copies must carry the SAME id or the consumer cannot tell they are the same
        // message. Generating it at publish time instead would defeat the whole guard.
        var message = new BasketCheckedOut(
            MessageId: Guid.CreateVersion7(),
            BasketId: basket.Id,
            CustomerId: basket.CustomerId,
            CustomerEmail: command.CustomerEmail,
            Total: basket.Total,
            Lines: [.. basket.Items.Select(item => new BasketCheckedOutLine(
                item.ProductId, item.ProductName, item.UnitPrice, item.Quantity))],
            CheckedOutAt: DateTimeOffset.UtcNow);

        var checkedOut = await outbox.Checkout(command.CustomerId, message, cancellationToken);

        if (!checkedOut)
        {
            // The basket changed between the read above and the atomic checkout — most
            // likely a second checkout request won. Reporting a conflict is better than
            // retrying, which could check out a basket the customer has since added to.
            return Result<CheckoutResult>.Fail(Error.Conflict(
                "Basket.CheckoutConflict",
                "The basket changed during checkout. Reload it and try again."));
        }

        return Result<CheckoutResult>.Success(
            new CheckoutResult(message.MessageId, message.Total, message.Lines.Count));
    }
}
