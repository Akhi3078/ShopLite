using Microsoft.Extensions.DependencyInjection;
using Ordering.Application.Abstractions;
using Ordering.Application.Orders.Commands;
using Ordering.Application.Orders.Queries;

namespace Ordering.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddOrderingApplication(this IServiceCollection services)
    {
        services.AddScoped<ICommandHandler<PlaceOrderFromBasketCommand, PlaceOrderResult>, PlaceOrderFromBasketHandler>();

        // The same handler behind a non-generic interface, for the message edge. See
        // IPlaceOrderFromBasket for why Wolverine needs it.
        services.AddScoped<IPlaceOrderFromBasket, PlaceOrderFromBasketHandler>();

        services.AddScoped<IQueryHandler<GetOrdersQuery, IReadOnlyList<OrderSummary>>, GetOrdersHandler>();
        services.AddScoped<IQueryHandler<GetOrderByIdQuery, OrderDetail>, GetOrderByIdHandler>();

        return services;
    }
}
