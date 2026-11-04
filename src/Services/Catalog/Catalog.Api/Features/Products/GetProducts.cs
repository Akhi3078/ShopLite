using Catalog.Api.Common;
using Catalog.Api.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Features.Products;

/// <summary>GET /products — paged list, optionally filtered by category.</summary>
public static class GetProducts
{
    public sealed record ProductSummary(Guid Id, string Name, decimal Price, string Category);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/", Handle)
            .WithName(nameof(GetProducts))
            .WithSummary("List products")
            .WithDescription("Returns a page of products, newest first, optionally filtered by category.");

    private static async Task<Ok<PagedResult<ProductSummary>>> Handle(
        CatalogDbContext db,
        CancellationToken cancellationToken,
        string? category = null,
        int page = 1,
        int pageSize = 20)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        // AsNoTracking: this is a read. The change tracker would be pure overhead.
        var query = db.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(p => p.Category == category);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductSummary(p.Id, p.Name, p.Price, p.Category))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new PagedResult<ProductSummary>(items, page, pageSize, totalCount));
    }
}
