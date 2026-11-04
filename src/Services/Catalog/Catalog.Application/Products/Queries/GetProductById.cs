using Catalog.Application.Abstractions;
using Catalog.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Application.Products.Queries;

public sealed record GetProductByIdQuery(Guid Id);

public sealed record ProductDetail(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    string Category,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed class GetProductByIdHandler(ICatalogDbContext db)
    : IQueryHandler<GetProductByIdQuery, ProductDetail>
{
    public async Task<Result<ProductDetail>> Handle(
        GetProductByIdQuery query, CancellationToken cancellationToken)
    {
        var product = await db.Products
            .AsNoTracking()
            .Where(p => p.Id == query.Id)
            .Select(p => new ProductDetail(
                p.Id, p.Name, p.Description, p.Price, p.Category, p.CreatedAt, p.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return product is null
            ? Result<ProductDetail>.Fail(
                Error.NotFound("Product.NotFound", $"No product with id {query.Id}."))
            : Result<ProductDetail>.Success(product);
    }
}
