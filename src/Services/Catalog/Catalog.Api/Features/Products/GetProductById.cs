using Catalog.Api.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Features.Products;

/// <summary>GET /products/{id} — full detail for one product.</summary>
public static class GetProductById
{
    public sealed record ProductDetail(
        Guid Id,
        string Name,
        string Description,
        decimal Price,
        string Category,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/{id:guid}", Handle)
            .WithName(nameof(GetProductById))
            .WithSummary("Get a product by id");

    private static async Task<Results<Ok<ProductDetail>, NotFound>> Handle(
        Guid id,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var product = await db.Products
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new ProductDetail(
                p.Id, p.Name, p.Description, p.Price, p.Category, p.CreatedAt, p.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return product is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(product);
    }
}
