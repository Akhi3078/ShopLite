using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Basket.Application.Abstractions;
using Basket.Application.Common;
using Basket.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Basket.Infrastructure.Catalog;

/// <summary>
/// Reads prices from Catalog over HTTP, and keeps working when Catalog does not.
/// </summary>
/// <remarks>
/// The resilience pipeline (timeout, retry, circuit breaker) is configured where the
/// client is registered, in DependencyInjection. It stops transient faults from reaching
/// this code at all. What it cannot do is invent an answer once Catalog is genuinely
/// down — a circuit breaker fails fast, it does not fail softly. That is this class's
/// job: fall back to the last price it saw, and mark the result as cached so the caller
/// knows the number is remembered rather than current.
/// </remarks>
internal sealed class CatalogHttpPriceReader(
    HttpClient http,
    IConnectionMultiplexer redis,
    ICallerContext caller,
    ILogger<CatalogHttpPriceReader> logger) : ICatalogPriceReader
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private IDatabase Database => redis.GetDatabase();

    public async Task<Result<ProductPrice>> GetPrice(Guid productId, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/products/{productId}");

            // Catalog's endpoints require authentication, so the caller's own token is
            // forwarded. This is token relay: simple, and it preserves the caller's
            // identity end to end, but it also means Basket can do anything the caller
            // can. ADR 0006 records why that is acceptable now and what replaces it.
            if (caller.AccessToken is { Length: > 0 } token)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            using var response = await http.SendAsync(request, cancellationToken);

            // A missing product is an answer, not a failure: Catalog is healthy and has
            // told us this id does not exist. Retrying or falling back would both be wrong.
            if (response.StatusCode is HttpStatusCode.NotFound)
            {
                return Result<ProductPrice>.Fail(Error.NotFound(
                    "Catalog.ProductNotFound", $"Catalog has no product with id {productId}."));
            }

            response.EnsureSuccessStatusCode();

            var product = await response.Content
                .ReadFromJsonAsync<CatalogProduct>(cancellationToken);

            if (product is null)
            {
                return Result<ProductPrice>.Fail(Error.Unavailable(
                    "Catalog.EmptyResponse", "Catalog returned an empty response body."));
            }

            await RememberPrice(product);

            return Result<ProductPrice>.Success(
                new ProductPrice(product.Id, product.Name, product.Price, FromCache: false));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller gave up. Not a Catalog fault, so it must not look like one.
            throw;
        }
        catch (Exception exception)
        {
            // Everything else means the same thing here: no answer from Catalog right now.
            // That covers a refused connection, a DNS failure, an exhausted retry budget,
            // a timeout, and Polly's BrokenCircuitException once the breaker has opened.
            // They are caught as one group on purpose — distinguishing them would couple
            // this class to Polly's exception types for no behavioural difference.
            logger.LogWarning(
                exception,
                "Catalog unreachable while pricing product {ProductId}. Trying the fallback cache.",
                productId);

            return await PriceFromCache(productId);
        }
    }

    /// <summary>
    /// Write-through: every successful read refreshes the fallback cache, with a TTL.
    /// </summary>
    /// <remarks>
    /// The TTL is the part the SQL Server version could not express. There, a cached price
    /// lived forever and the fallback could serve something arbitrarily old; here a price
    /// Catalog has not confirmed within <see cref="RedisKeys.PriceLifetime"/> simply stops
    /// existing, and the fallback fails honestly instead of answering with it.
    ///
    /// This is also a plain write now rather than something deferred to a caller's
    /// SaveChanges, which removes the ordering coupling the EF version needed.
    /// </remarks>
    private async Task RememberPrice(CatalogProduct product)
    {
        var record = new PriceRecord(product.Id, product.Name, product.Price, DateTimeOffset.UtcNow);

        await Database.StringSetAsync(
            RedisKeys.Price(product.Id),
            JsonSerializer.Serialize(record, SerializerOptions),
            RedisKeys.PriceLifetime);
    }

    private async Task<Result<ProductPrice>> PriceFromCache(Guid productId)
    {
        var stored = await Database.StringGetAsync(RedisKeys.Price(productId));

        if (stored.IsNullOrEmpty)
        {
            // Nothing cached and nobody to ask. Failing here is the honest outcome: the
            // alternative is guessing a price, and a wrong price is worse than an error.
            return Result<ProductPrice>.Fail(Error.Unavailable(
                "Catalog.Unavailable",
                $"Catalog is unavailable and no cached price exists for product {productId}."));
        }

        var cached = JsonSerializer.Deserialize<PriceRecord>(stored.ToString(), SerializerOptions);

        if (cached is null)
        {
            return Result<ProductPrice>.Fail(Error.Unavailable(
                "Catalog.Unavailable",
                $"Catalog is unavailable and the cached price for product {productId} could not be read."));
        }

        logger.LogInformation(
            "Priced product {ProductId} from cache, last refreshed {CachedAt:u}.",
            productId,
            cached.CachedAt);

        return Result<ProductPrice>.Success(
            new ProductPrice(cached.ProductId, cached.Name, cached.UnitPrice, FromCache: true));
    }

    /// <summary>
    /// Only the three fields Basket needs from Catalog's product representation.
    /// </summary>
    /// <remarks>
    /// Not a shared contract package: Catalog can add fields without Basket recompiling,
    /// and Basket's view of a product stays as small as its actual need.
    /// </remarks>
    private sealed record CatalogProduct(Guid Id, string Name, decimal Price);
}
