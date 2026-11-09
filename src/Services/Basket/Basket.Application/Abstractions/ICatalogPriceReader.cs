using Basket.Application.Common;

namespace Basket.Application.Abstractions;

/// <summary>
/// What Basket needs from Catalog: a name and a price for one product.
/// </summary>
/// <remarks>
/// This is the only service-to-service dependency in ShopLite, and it is expressed as a
/// port so the Application layer never learns that it is satisfied over HTTP. The
/// implementation owns the timeout, retry, circuit breaker and fallback cache; handlers
/// see only success, NotFound, or Unavailable.
/// </remarks>
public interface ICatalogPriceReader
{
    Task<Result<ProductPrice>> GetPrice(Guid productId, CancellationToken cancellationToken);
}

/// <param name="FromCache">
/// True when Catalog was unreachable and this came from Basket's own last-known-price
/// cache. Carried all the way to the HTTP response so degradation is never silent.
/// </param>
public sealed record ProductPrice(Guid ProductId, string Name, decimal UnitPrice, bool FromCache);
