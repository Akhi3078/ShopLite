using Ordering.Application.Abstractions;
using Ordering.Application.Common;
using Ordering.Application.Orders.Commands;
using ShopLite.Contracts;

namespace Ordering.Api.Messaging;

/// <summary>
/// Receives <see cref="BasketCheckedOut"/> from Service Bus and turns it into an order.
/// </summary>
/// <remarks>
/// Wolverine finds this by convention: a public class whose name ends in Handler with a
/// public Handle method taking the message type. No registration, no interface.
///
/// <para>
/// It is deliberately thin, and in exactly the same way as an HTTP endpoint: translate the
/// incoming shape into a command, hand it to the Application layer, decide what the
/// transport should do with the outcome. The business rules live in
/// <see cref="PlaceOrderFromBasketHandler"/>, which has no idea a broker exists — which is
/// why the same use case could later be driven by an HTTP call or a replayed event with no
/// changes at all.
/// </para>
/// </remarks>
/// <remarks>
/// Dependencies arrive through the <b>constructor</b>, not as extra Handle parameters.
///
/// Wolverine generates the code that calls this method and resolves method parameters at
/// generation time. It cannot do that for a closed generic like
/// <c>ICommandHandler&lt;,&gt;</c>, so it falls back to service location — which its
/// default <c>ServiceLocationPolicy.NotAllowed</c> rejects. The result is
/// <c>InvalidServiceLocationException</c>, thrown while handling the message rather than
/// at startup, so the message retries three times and dead-letters while the service
/// itself looks perfectly healthy.
///
/// Constructor injection sidesteps it: the container builds the handler, and containers
/// have no trouble with generics.
/// </remarks>
public class BasketCheckedOutHandler(
    IPlaceOrderFromBasket handler,
    ILogger<BasketCheckedOutHandler> logger)
{
    public async Task Handle(BasketCheckedOut message, CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new PlaceOrderFromBasketCommand(message), cancellationToken);

        if (result.IsSuccess)
        {
            if (result.Value!.AlreadyHandled)
            {
                // A redelivery. Returning normally acknowledges the message so the broker
                // stops sending it. Throwing here would be the classic mistake: the
                // message would retry, fail the same way, and eventually dead-letter —
                // turning successful duplicate detection into a false alarm.
                return;
            }

            logger.LogInformation(
                "Order {OrderId} created from basket {BasketId}.", result.Value.OrderId, message.BasketId);

            return;
        }

        var error = result.Error!;

        // A validation failure is not transient: the message itself is wrong, so every
        // retry produces the identical result. Dead-lettering it immediately is both
        // faster and more honest than three retries and a cooldown.
        //
        // This distinction — is the message bad, or was the system briefly unable to
        // handle a good message — is the one that decides whether retrying is useful.
        // Wolverine cannot make it; only this code knows.
        if (error.Type is ErrorType.Validation)
        {
            logger.LogError(
                "Message {MessageId} is not processable and will be dead-lettered: {Code} {Message}",
                message.MessageId,
                error.Code,
                error.Message);

            throw new InvalidOperationException(
                $"Unprocessable {nameof(BasketCheckedOut)} {message.MessageId}: {error.Message}");
        }

        // Anything else is assumed transient — a database that was briefly unreachable,
        // for instance. Throwing hands it back to the retry policy in ServiceDefaults,
        // which waits 200ms, then 1s, then 5s before giving up to the dead-letter queue.
        logger.LogWarning(
            "Message {MessageId} failed and will be retried: {Code} {Message}",
            message.MessageId,
            error.Code,
            error.Message);

        throw new InvalidOperationException(
            $"Failed to handle {nameof(BasketCheckedOut)} {message.MessageId}: {error.Message}");
    }
}
