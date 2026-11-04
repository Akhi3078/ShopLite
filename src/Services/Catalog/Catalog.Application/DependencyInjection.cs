using Catalog.Application.Abstractions;
using Catalog.Application.Common;
using Catalog.Application.Products.Commands;
using Catalog.Application.Products.Queries;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Catalog.Application;

/// <summary>
/// Registers this layer's use cases. Each layer owns its own registration so the Api
/// composition root stays a list of layers rather than a list of every type in the service.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddCatalogApplication(this IServiceCollection services)
    {
        // Registered explicitly rather than by assembly scanning. Verbose, but every
        // dispatch is traceable to a line of code, which matters more while learning
        // than the typing saved. Scanning is a sensible later refactor.
        services.AddScoped<ICommandHandler<CreateProductCommand, Guid>, CreateProductHandler>();
        services.AddScoped<ICommandHandler<UpdateProductCommand>, UpdateProductHandler>();
        services.AddScoped<ICommandHandler<DeleteProductCommand>, DeleteProductHandler>();

        services.AddScoped<IQueryHandler<GetProductsQuery, PagedResult<ProductSummary>>, GetProductsHandler>();
        services.AddScoped<IQueryHandler<GetProductByIdQuery, ProductDetail>, GetProductByIdHandler>();

        services.AddScoped<IValidator<CreateProductCommand>, CreateProductValidator>();
        services.AddScoped<IValidator<UpdateProductCommand>, UpdateProductValidator>();

        return services;
    }
}
