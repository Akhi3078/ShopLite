using Catalog.Api.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Features.Products;

/// <summary>PUT /products/{id} — replace a product's details.</summary>
public static class UpdateProduct
{
    public sealed record Request(string Name, string Description, decimal Price, string Category);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPut("/{id:guid}", Handle)
            .WithName(nameof(UpdateProduct))
            .WithSummary("Update a product");

    private static async Task<Results<NoContent, NotFound, ValidationProblem>> Handle(
        Guid id,
        Request request,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var errors = ProductValidation.Validate(
            request.Name, request.Description, request.Price, request.Category);

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        // Tracked on purpose: this is a write, and the entity enforces its own invariants
        // through Update() rather than being mutated property by property from here.
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
        {
            return TypedResults.NotFound();
        }

        product.Update(request.Name, request.Description, request.Price, request.Category);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }
}
