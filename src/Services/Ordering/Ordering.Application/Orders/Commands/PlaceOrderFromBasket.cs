using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ordering.Application.Abstractions;
using Ordering.Application.Common;
using Ordering.Domain.Entities;
using ShopLite.Contracts;

namespace Ordering.Application.Orders.Commands;

/// <summary>
/// Creates an order from a checked-out basket.
/// </summary>
/// <remarks>
/// The command carries the message itself rather than unpacked fields, because this use
/// case exists only to serve that one message and pretending otherwise would add a mapping
/// nobody reads. The transport is still absent: nothing here knows about Service Bus,
/// topics, or how the message arrived.
/// </remarks>
public sealed record PlaceOrderFromBasketCommand(BasketCheckedOut Message);

/// <param name="AlreadyHandled">
/// True when this message had already been processed, so nothing was written. The caller
/// needs to know, because the correct response to a duplicate is to acknowledge it, not
/// to retry it.
/// </param>
public sealed record PlaceOrderResult(Guid OrderId, bool AlreadyHandled);

/// <summary>
/// A non-generic face on this one use case, for the message handler to depend on.
/// </summary>
/// <remarks>
/// <c>ICommandHandler&lt;TCommand, TResult&gt;</c> is the right shape for HTTP endpoints,
/// where ASP.NET Core resolves parameters from the container at request time and generics
/// cost nothing. Wolverine is different: it <i>generates</i> the code that calls a handler
/// and tries to construct dependencies inline, which it cannot do for a closed generic. It
/// falls back to service location, and its default policy rejects that — throwing while
/// handling a message rather than at startup, so every message retries and dead-letters
/// while the service reports itself healthy.
///
/// One extra interface avoids all of that, and reads better at the edge besides: the
/// message handler asks for the thing it needs by name instead of by type parameters.
/// </remarks>
public interface IPlaceOrderFromBasket
{
    Task<Result<PlaceOrderResult>> Handle(
        PlaceOrderFromBasketCommand command, CancellationToken cancellationToken);
}

public sealed class PlaceOrderFromBasketHandler(
    IOrderingDbContext db,
    ILogger<PlaceOrderFromBasketHandler> logger)
    : ICommandHandler<PlaceOrderFromBasketCommand, PlaceOrderResult>, IPlaceOrderFromBasket
{
    public async Task<Result<PlaceOrderResult>> Handle(
        PlaceOrderFromBasketCommand command, CancellationToken cancellationToken)
    {
        var message = command.Message;

        if (message.Lines.Count == 0)
        {
            // Not retryable, so it must not be reported as a failure: a retry would fail
            // identically three times and then dead-letter. An empty basket should never
            // have been published, but refusing it cleanly beats looping on it.
            return Result<PlaceOrderResult>.Fail(Error.Validation(
                new Dictionary<string, string[]>
                {
                    ["Lines"] = ["A checked-out basket must contain at least one line."]
                }));
        }

        // First half of the idempotency guard: cheap, and catches the common case where a
        // redelivery arrives after the first attempt has finished.
        var existing = await db.ProcessedMessages
            .AsNoTracking()
            .FirstOrDefaultAsync(processed => processed.MessageId == message.MessageId, cancellationToken);

        if (existing is not null)
        {
            // Deliberately not an error. The broker did its job, we have already done
            // ours, and the only sane outcome is a quiet success so the message is
            // acknowledged and never seen again.
            logger.LogInformation(
                "Message {MessageId} was already handled at {HandledAt:u}; ignoring the redelivery.",
                message.MessageId,
                existing.HandledAt);

            return Result<PlaceOrderResult>.Success(new PlaceOrderResult(Guid.Empty, AlreadyHandled: true));
        }

        var order = Order.Place(
            message.CustomerId,
            message.CustomerEmail,
            message.Total,
            message.CheckedOutAt,
            message.Lines.Select(line =>
                (line.ProductId, line.ProductName, line.UnitPrice, line.Quantity)));

        db.Orders.Add(order);
        db.ProcessedMessages.Add(ProcessedMessage.Create(message.MessageId, nameof(BasketCheckedOut)));

        try
        {
            // One SaveChanges, therefore one transaction: the order and the record that
            // this message produced it commit together. Writing the marker separately
            // would leave a window where a crash either loses the marker (so the message
            // is processed twice) or loses the order (so it is never processed at all).
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.Entries.Any(entry => entry.Entity is ProcessedMessage))
        {
            // Second half of the guard. Two deliveries of the same message can both pass
            // the check above and race to here; the primary key settles it. The loser
            // reports success for the same reason as the early return — the work is done.
            //
            // The filter matters: only a collision on the marker means "already handled".
            // Catching every DbUpdateException here would silently swallow real
            // persistence failures and acknowledge messages that were never processed.
            logger.LogInformation(
                "Message {MessageId} was handled concurrently by another consumer; ignoring.",
                message.MessageId);

            return Result<PlaceOrderResult>.Success(new PlaceOrderResult(Guid.Empty, AlreadyHandled: true));
        }

        logger.LogInformation(
            "Placed order {OrderId} for customer {CustomerId} from message {MessageId}.",
            order.Id,
            message.CustomerId,
            message.MessageId);

        return Result<PlaceOrderResult>.Success(new PlaceOrderResult(order.Id, AlreadyHandled: false));
    }
}
