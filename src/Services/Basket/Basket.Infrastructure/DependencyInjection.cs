using Basket.Application.Abstractions;
using Basket.Infrastructure.Catalog;
using Basket.Infrastructure.Messaging;
using Basket.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;

namespace Basket.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// The service-discovery name Aspire assigns to Catalog, not a hostname.
    /// </summary>
    /// <remarks>
    /// <c>https+http</c> means "prefer HTTPS, accept HTTP". Service discovery resolves
    /// this at request time from configuration the AppHost injects, so Catalog can move
    /// ports, scale to several replicas, or run in a container without Basket changing.
    /// Hardcoding https://localhost:7232 would work today and break at the first of those.
    /// </remarks>
    private const string CatalogServiceName = "https+http://catalog";

    public static IHostApplicationBuilder AddBasketInfrastructure(this IHostApplicationBuilder builder)
    {
        // Registers IConnectionMultiplexer from the "basketcache" connection string the
        // AppHost injects, and adds a readiness health check and telemetry for it.
        builder.AddRedisClient("basketcache");

        // The multiplexer is a singleton by design — it owns the connection and multiplexes
        // every command over it — so the repository holding one can be a singleton too.
        // This is the opposite of a DbContext, which must be scoped because it accumulates
        // per-request change-tracking state. Redis keeps no such state between calls.
        builder.Services.AddSingleton<IBasketRepository, RedisBasketRepository>();

        // The outbox and its publisher. Singletons for the same reason the repository is:
        // the Redis multiplexer holds no per-request state.
        builder.Services.AddSingleton<IBasketOutbox, RedisBasketOutbox>();
        builder.Services.AddHostedService<OutboxPublisher>();

        var catalogClient = builder.Services
            .AddHttpClient<ICatalogPriceReader, CatalogHttpPriceReader>(client =>
                client.BaseAddress = new Uri(CatalogServiceName));

        // ServiceDefaults adds a standard resilience handler to every client in the
        // process. Leaving it in place and adding another would stack two pipelines:
        // 3 retries inside 3 retries is 9 calls to a service that is already struggling,
        // which is how a retry storm turns a blip into an outage.
        //
        // The API is flagged experimental, and that is accepted here deliberately. The
        // alternative — reaching into the named options of a handler registered in another
        // project — is quieter but far more likely to break silently if ServiceDefaults
        // changes. If this call disappears in a future release the compiler will say so,
        // and this comment explains what it was for.
#pragma warning disable EXTEXP0001
        catalogClient.RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001

        catalogClient.AddStandardResilienceHandler(options =>
        {
            // Per attempt. Catalog answers a keyed lookup in milliseconds, so a wait of
            // seconds means something is wrong, not slow.
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);

            // Across all attempts, including the waits between them. Without this a caller
            // can be held far longer than any single timeout suggests.
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(12);

            // Two retries, fast. This GET is safe to repeat; a POST would not be, which is
            // the distinction ProcessedRequest exists to handle.
            options.Retry.MaxRetryAttempts = 2;
            options.Retry.Delay = TimeSpan.FromMilliseconds(200);

            // Stop retrying a service that is down. Once half the calls in a 10-second
            // window fail, the circuit opens for 15 seconds and every call fails
            // immediately — which is what makes the cached fallback fast, instead of
            // making each request wait out its own timeout first.
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(10);
            options.CircuitBreaker.FailureRatio = 0.5;
            options.CircuitBreaker.MinimumThroughput = 3;
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
        });

        return builder;
    }
}
