using Catalog.Api.Domain;
using Catalog.Api.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Catalog.Api.Features.Products;

/// <summary>POST /products — add a product.</summary>
public static class CreateProduct
{
    public sealed record Request(string Name, string Description, decimal Price, string Category);

    public sealed record Response(Guid Id);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/", Handle)
            .WithName(nameof(CreateProduct))
            .WithSummary("Create a product");

    private static async Task<Results<Created<Response>, ValidationProblem>> Handle(
        Request request,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        // Validation is hand-rolled while the rules are this thin. FluentValidation
        // earns its place once rules become conditional or cross-field.
        var errors = ProductValidation.Validate(
            request.Name, request.Description, request.Price, request.Category);

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var product = Product.Create(request.Name, request.Description, request.Price, request.Category);

        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Created($"/products/{product.Id}", new Response(product.Id));
    }
}
