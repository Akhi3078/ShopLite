using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.AzureServiceBus;
using Wolverine.ErrorHandling;
using Wolverine.RabbitMQ;

namespace ShopLite.ServiceDefaults;

/// <summary>
/// The messaging settings every service shares, so transport choice and failure policy
/// cannot drift between them.
/// </summary>
/// <remarks>
/// Only transport and failure policy live here — never a handler, and never a message
/// type. Those belong to the service that owns them, and to ShopLite.Contracts.
///
/// <para>
/// <b>Two transports, one abstraction.</b> RabbitMQ locally, Azure Service Bus when
/// deployed. That split is not ideal — the thing you test is not quite the thing you run —
/// and it was forced: the Azure Service Bus emulator cannot deliver a topic message
/// without a subscription rule, and crashes if given one ([ADR 0008]).
/// </para>
///
/// <para>
/// The silver lining is that having two transports proves the application code is actually
/// transport-agnostic. Not one handler, message or endpoint changed when this was swapped
/// in; everything that needed to know is in this file.
/// </para>
/// </remarks>
public static class MessagingExtensions
{
    /// <summary>The Aspire resource name of the broker, whichever one is running.</summary>
    public const string BusConnectionName = "messaging";

    /// <summary>
    /// The event's destination: a Service Bus topic, or a RabbitMQ fanout exchange. The
    /// same name is used for both, which keeps the two topologies readable side by side.
    /// </summary>
    public const string BasketCheckedOutTopic = "basket-checked-out";

    /// <summary>
    /// Ordering's subscription. On Service Bus this is a subscription on the topic; on
    /// RabbitMQ it becomes a queue bound to the exchange.
    /// </summary>
    /// <remarks>
    /// Named "ordering-service" rather than "ordering" because Aspire's resource names are
    /// unique across <i>all</i> resource types: a subscription called "ordering" collides
    /// with the project called "ordering" and the AppHost refuses to start.
    /// </remarks>
    public const string OrderingSubscription = "ordering-service";

    /// <summary>The RabbitMQ queue Ordering reads, bound to the fanout exchange.</summary>
    public const string OrderingQueue = $"{BasketCheckedOutTopic}.{OrderingSubscription}";

    public static WolverineOptions UseShopLiteMessaging(
        this WolverineOptions options, IHostApplicationBuilder builder)
    {
        var connectionString = ConnectionString(builder);

        if (IsRabbitMq(connectionString))
        {
            options.UseRabbitMq(new Uri(connectionString))

                // Safe and useful here, unlike on the Service Bus emulator: RabbitMQ
                // declares exchanges, queues and bindings over AMQP itself, the
                // operations are idempotent, and Aspire recreates the container each run
                // anyway. Against real Azure, infrastructure belongs in deployment.
                .AutoProvision()

                // A fanout exchange is the RabbitMQ equivalent of a Service Bus topic:
                // every bound queue gets its own copy. A direct exchange would deliver to
                // one consumer and quietly turn publish/subscribe into a work queue.
                .DeclareExchange(BasketCheckedOutTopic, exchange => exchange.ExchangeType = ExchangeType.Fanout)

                // One queue per consumer, bound to the exchange. Adding a second consumer
                // means another queue and another binding — the publisher never changes.
                .BindExchange(BasketCheckedOutTopic)
                .ToQueue(OrderingQueue);
        }
        else
        {
            options.UseAzureServiceBus(connectionString)

                // Wolverine otherwise declares a private reply queue and retry queue per
                // service. The emulator cannot create them and they are not in the
                // declared topology, so every receive loop logs "entity does not exist".
                // Nothing here uses request/reply, so they are turned off.
                .SystemQueuesAreEnabled(false);
        }

        // ── Failure policy, identical on both transports ─────────────────────────────
        //
        // Three attempts with widening gaps, then stop. The pauses matter: an immediate
        // retry of a message that failed because a database was briefly unreachable just
        // fails again, three times, and then the message is gone.
        options.OnException<Exception>()
            .RetryWithCooldown(TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5))
            .Then.MoveToErrorQueue();

        // A message that still fails after that is a *poison message*: something about it,
        // not about the system, makes it unprocessable — a malformed payload, a reference
        // to something deleted, a bug only this message triggers. Left in place it blocks
        // everything behind it, which is how one bad message stops a queue. The
        // dead-letter queue keeps the queue flowing and leaves the message intact for
        // inspection: parked, not discarded.
        options.Policies.OnAnyException().MoveToErrorQueue();

        return options;
    }

    /// <summary>
    /// Routes <c>BasketCheckedOut</c> to the broker. Called by the publishing service.
    /// </summary>
    /// <remarks>
    /// <b>SendInline is load-bearing, not a tuning knob.</b> By default Wolverine buffers
    /// outgoing messages: <c>PublishAsync</c> hands the message to an in-memory queue and
    /// returns, and the broker send happens afterwards. That breaks the outbox completely —
    /// the publisher deletes its durable copy as soon as <c>PublishAsync</c> returns, so a
    /// send failing after that point loses the message with nothing to retry from and no
    /// error anywhere. Inline sending makes <c>PublishAsync</c> wait for the broker to
    /// accept, and only then does the outbox forget.
    /// </remarks>
    public static WolverineOptions PublishBasketCheckedOut<TMessage>(
        this WolverineOptions options, IHostApplicationBuilder builder)
    {
        var connectionString = ConnectionString(builder);

        if (IsRabbitMq(connectionString))
        {
            options.PublishMessage<TMessage>()
                .ToRabbitExchange(BasketCheckedOutTopic)
                .SendInline();
        }
        else
        {
            options.PublishMessage<TMessage>()
                .ToAzureServiceBusTopic(BasketCheckedOutTopic)
                .SendInline();
        }

        return options;
    }

    /// <summary>
    /// Subscribes to <c>BasketCheckedOut</c>. Called by the consuming service.
    /// </summary>
    public static WolverineOptions ListenForBasketCheckedOut(
        this WolverineOptions options, IHostApplicationBuilder builder, int listenerCount = 3)
    {
        var connectionString = ConnectionString(builder);

        // Several messages at once, but each one once. Ordering keeps no cross-message
        // state, so parallelism is safe — and the idempotency guard means two deliveries
        // of the same message racing each other still produce one order.
        if (IsRabbitMq(connectionString))
        {
            options.ListenToRabbitQueue(OrderingQueue).ListenerCount(listenerCount);
        }
        else
        {
            options.ListenToAzureServiceBusSubscription(OrderingSubscription)
                .FromTopic(BasketCheckedOutTopic)
                .ListenerCount(listenerCount);
        }

        return options;
    }

    private static string ConnectionString(IHostApplicationBuilder builder) =>
        builder.Configuration.GetConnectionString(BusConnectionName)
        ?? throw new InvalidOperationException(
            $"Connection string '{BusConnectionName}' is not configured. The AppHost supplies it.");

    /// <summary>
    /// Which broker is on the other end, decided from the connection string itself.
    /// </summary>
    /// <remarks>
    /// RabbitMQ gives <c>amqp://...</c>; Service Bus gives <c>Endpoint=sb://...</c>. Using
    /// the connection string rather than a separate setting means there is one source of
    /// truth: a service cannot be pointed at RabbitMQ while being configured for Service
    /// Bus, because the same value decides both.
    /// </remarks>
    private static bool IsRabbitMq(string connectionString) =>
        connectionString.StartsWith("amqp", StringComparison.OrdinalIgnoreCase);
}
