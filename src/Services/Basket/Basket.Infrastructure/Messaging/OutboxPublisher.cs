using System.Text.Json;
using Basket.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShopLite.Contracts;
using StackExchange.Redis;
using Wolverine;

namespace Basket.Infrastructure.Messaging;

/// <summary>
/// Drains the outbox: takes each recorded event and publishes it to Service Bus.
/// </summary>
/// <remarks>
/// The second half of the outbox pattern. Checkout wrote the event durably and returned
/// immediately; this loop does the part that is allowed to be slow and allowed to fail,
/// because the event is already safe.
///
/// <para>
/// The move from pending to in-flight uses <c>RPOPLPUSH</c>, which is atomic. That matters
/// for the one failure this has to survive: if the process dies after publishing but
/// before removing the in-flight copy, the message is republished on restart. The result
/// is <b>at-least-once</b> delivery — never exactly-once, which is not achievable here —
/// and the reason the consumer deduplicates on message id.
/// </para>
///
/// <para>
/// A crash <i>before</i> publishing is also covered: the entry sits in the in-flight list
/// and is recovered on the next startup. What is deliberately missing is a reaper for
/// entries stranded by a process that never comes back; with one instance that is the
/// same thing as startup recovery, and with several it would not be. Noted as a known
/// limitation rather than quietly assumed away.
/// </para>
/// </remarks>
internal sealed class OutboxPublisher(
    IConnectionMultiplexer redis,
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    // IMessageBus is scoped, and this is a singleton. Injecting it directly is a captive
    // dependency — a scoped service pinned for the lifetime of the process — and .NET's
    // DI validation rejects it outright at startup rather than letting it misbehave
    // later. A scope per publish is the correct shape: it also means each published
    // message gets its own Wolverine context rather than sharing one forever.
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// How long to wait when the outbox is empty. Short enough that checkout feels
    /// immediate, long enough not to hammer Redis — and the reason a real system would
    /// use a notification rather than polling.
    /// </summary>
    private static readonly TimeSpan IdleDelay = TimeSpan.FromMilliseconds(250);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var database = redis.GetDatabase();

        await RecoverInFlight(database, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Atomic claim: the entry leaves the pending list and appears on the
                // in-flight list in one operation, so it cannot be lost between the two
                // or picked up twice by concurrent publishers.
                var claimed = await database.ListRightPopLeftPushAsync(
                    RedisKeys.OutboxPending, RedisKeys.OutboxInFlight);

                if (claimed.IsNullOrEmpty)
                {
                    await Task.Delay(IdleDelay, stoppingToken);
                    continue;
                }

                await PublishClaimed(database, claimed!, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // Never let the loop die. An outbox that stops draining is silent and
                // total: checkouts keep succeeding and no order is ever created.
                logger.LogError(exception, "The outbox publisher loop failed; retrying shortly.");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    private async Task PublishClaimed(IDatabase database, RedisValue claimed, CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<BasketCheckedOut>(claimed.ToString(), SerializerOptions);

        if (message is null)
        {
            // Unreadable, so retrying will never help. Dropped from in-flight and logged
            // loudly rather than blocking the outbox forever.
            logger.LogError("Discarding an unreadable outbox entry: {Payload}", claimed.ToString());
            await database.ListRemoveAsync(RedisKeys.OutboxInFlight, claimed);

            return;
        }

        using (var scope = scopeFactory.CreateScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            await bus.PublishAsync(message);
        }

        // Only now is it safe to forget. Removing before publishing would turn a broker
        // outage into a lost order.
        await database.ListRemoveAsync(RedisKeys.OutboxInFlight, claimed);

        logger.LogInformation(
            "Published {MessageType} {MessageId} for basket {BasketId}.",
            nameof(BasketCheckedOut),
            message.MessageId,
            message.BasketId);
    }

    /// <summary>
    /// Returns anything left in-flight by a previous run to the pending list.
    /// </summary>
    /// <remarks>
    /// These are the entries whose fate is genuinely unknown: the process died somewhere
    /// between claiming and confirming, so they may or may not have been published.
    /// Republishing is the right choice, because a duplicate is recoverable by the
    /// consumer and a lost order is not.
    /// </remarks>
    private async Task RecoverInFlight(IDatabase database, CancellationToken cancellationToken)
    {
        var recovered = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var moved = await database.ListRightPopLeftPushAsync(
                RedisKeys.OutboxInFlight, RedisKeys.OutboxPending);

            if (moved.IsNullOrEmpty)
            {
                break;
            }

            recovered++;
        }

        if (recovered > 0)
        {
            logger.LogWarning(
                "Recovered {Count} outbox entries left in flight by a previous run; they may be republished.",
                recovered);
        }
    }
}
