using Catalog.Api.Extensions;
using Catalog.Application.Abstractions;
using Catalog.Application.Common;
using Catalog.Application.Products.Commands;
using Catalog.Application.Products.Queries;

namespace Catalog.Api.Endpoints;

/// <summary>
/// HTTP surface for products. Every endpoint does the same three things: build a command
/// or query, hand it to a handler, map the result. No business logic reaches this layer.
/// </summary>
public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProducts(this IEndpointRouteBuilder app)
    {
        var products = app.MapGroup("/products").WithTags("Products");

        products.MapGet("/", async (
                IQueryHandler<GetProductsQuery, PagedResult<ProductSummary>> handler,
                CancellationToken cancellationToken,
                string? category = null,
                int page = 1,
                int pageSize = 20) =>
            {
                var result = await handler.Handle(
                    new GetProductsQuery(category, page, pageSize), cancellationToken);

                return result.ToHttpResult();
            })
            .WithName("GetProducts")
            .WithSummary("List products")
            .WithDescription("Returns a page of products, newest first, optionally filtered by category.");

        products.MapGet("/{id:guid}", async (
                Guid id,
                IQueryHandler<GetProductByIdQuery, ProductDetail> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new GetProductByIdQuery(id), cancellationToken);
                return result.ToHttpResult();
            })
            .WithName("GetProductById")
            .WithSummary("Get a product by id");

        products.MapPost("/", async (
                CreateProductCommand command,
                ICommandHandler<CreateProductCommand, Guid> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(command, cancellationToken);

                // 201 needs a location header, which the generic mapper cannot supply.
                return result.IsSuccess
                    ? Results.Created($"/products/{result.Value}", new { id = result.Value })
                    : result.ToHttpResult();
            })
            .WithName("CreateProduct")
            .WithSummary("Create a product");

        products.MapPut("/{id:guid}", async (
                Guid id,
                UpdateProductRequest request,
                ICommandHandler<UpdateProductCommand> handler,
                CancellationToken cancellationToken) =>
            {
                var command = new UpdateProductCommand(
                    id, request.Name, request.Description, request.Price, request.Category);

                var result = await handler.Handle(command, cancellationToken);
                return result.ToHttpResult();
            })
            .WithName("UpdateProduct")
            .WithSummary("Update a product");

        products.MapDelete("/{id:guid}", async (
                Guid id,
                ICommandHandler<DeleteProductCommand> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new DeleteProductCommand(id), cancellationToken);
                return result.ToHttpResult();
            })
            .WithName("DeleteProduct")
            .WithSummary("Delete a product");

        return app;
    }

    /// <summary>
    /// The id comes from the route, so the request body must not carry one. This is the
    /// one place an Api-specific shape is worth the extra type: binding
    /// UpdateProductCommand directly would expose an Id field that is then ignored.
    /// </summary>
    private sealed record UpdateProductRequest(
        string Name, string Description, decimal Price, string Category);
}
