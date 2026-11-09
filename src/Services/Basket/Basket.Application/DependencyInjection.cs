using Basket.Application.Abstractions;
using Basket.Application.Baskets.Commands;
using Basket.Application.Baskets.Queries;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Basket.Application;

/// <summary>
/// Registers this layer's use cases, explicitly rather than by assembly scanning, for the
/// same reason as Catalog: every dispatch stays traceable to a line of code.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddBasketApplication(this IServiceCollection services)
    {
        services.AddScoped<ICommandHandler<AddItemCommand, BasketView>, AddItemHandler>();
        services.AddScoped<ICommandHandler<SetItemQuantityCommand, BasketView>, SetItemQuantityHandler>();
        services.AddScoped<ICommandHandler<RemoveItemCommand, BasketView>, RemoveItemHandler>();
        services.AddScoped<ICommandHandler<ClearBasketCommand>, ClearBasketHandler>();
        services.AddScoped<ICommandHandler<CheckoutCommand, CheckoutResult>, CheckoutHandler>();

        services.AddScoped<IQueryHandler<GetBasketQuery, BasketView>, GetBasketHandler>();

        services.AddScoped<IValidator<AddItemCommand>, AddItemValidator>();
        services.AddScoped<IValidator<SetItemQuantityCommand>, SetItemQuantityValidator>();

        return services;
    }
}
