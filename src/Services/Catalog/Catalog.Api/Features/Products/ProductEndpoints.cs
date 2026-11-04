namespace Catalog.Api.Features.Products;

/// <summary>
/// Composes the product feature slices into one route group. Each slice owns its own
/// route, request shape and handler; this file only decides where they hang.
/// </summary>
public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProducts(this IEndpointRouteBuilder app)
    {
        var products = app.MapGroup("/products").WithTags("Products");

        GetProducts.Map(products);
        GetProductById.Map(products);
        CreateProduct.Map(products);
        UpdateProduct.Map(products);
        DeleteProduct.Map(products);

        return app;
    }
}
