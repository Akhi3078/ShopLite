using Catalog.Application.Abstractions;
using Catalog.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Application.Products.Queries;

public sealed record GetProductsQuery(string? Category = null, int Page = 1, int PageSize = 20);

public sealed record ProductSummary(Guid Id, string Name, decimal Price, string Category);

public sealed class GetProductsHandler(ICatalogDbContext db)
    : IQueryHandler<GetProductsQuery, PagedResult<ProductSummary>>
{
    public async Task<Result<PagedResult<ProductSummary>>> Handle(
        GetProductsQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        // AsNoTracking: this is a read. The change tracker would be pure overhead.
        var products = db.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            products = products.Where(product => product.Category == query.Category);
        }

        var totalCount = await products.CountAsync(cancellationToken);

        var items = await products
            .OrderByDescending(product => product.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(product => new ProductSummary(
                product.Id, product.Name, product.Price, product.Category))
            .ToListAsync(cancellationToken);

        return Result<PagedResult<ProductSummary>>.Success(
            new PagedResult<ProductSummary>(items, page, pageSize, totalCount));
    }
}
