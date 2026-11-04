using Catalog.Api.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Features.Products;

/// <summary>DELETE /products/{id}</summary>
public static class DeleteProduct
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/{id:guid}", Handle)
            .WithName(nameof(DeleteProduct))
            .WithSummary("Delete a product");

    private static async Task<Results<NoContent, NotFound>> Handle(
        Guid id,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var deleted = await db.Products
            .Where(p => p.Id == id)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted == 0
            ? TypedResults.NotFound()
            : TypedResults.NoContent();
    }
}
