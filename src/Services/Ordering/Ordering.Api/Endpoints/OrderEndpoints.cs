using System.Security.Claims;
using Ordering.Api.Extensions;
using Ordering.Application.Abstractions;
using Ordering.Application.Orders.Queries;
using ShopLite.ServiceDefaults;

namespace Ordering.Api.Endpoints;

/// <summary>
/// Read-only HTTP surface for orders.
/// </summary>
/// <remarks>
/// There is deliberately no <c>POST /orders</c>. An order is not created by asking this
/// service to create one — it is created by consuming <c>BasketCheckedOut</c>. Adding a
/// POST would give the system two ways to produce an order, only one of which goes through
/// checkout, and the two would drift.
/// </remarks>
public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrders(this IEndpointRouteBuilder app)
    {
        var orders = app.MapGroup("/orders")
            .WithTags("Orders")
            .RequireAuthorization(Policies.Authenticated);

        orders.MapGet("/", async (
                ClaimsPrincipal user,
                IQueryHandler<GetOrdersQuery, IReadOnlyList<OrderSummary>> handler,
                CancellationToken cancellationToken) =>
            {
                // An Admin sees every order; everyone else sees only their own. The scope
                // is decided here, from the token, and passed down — the query itself has
                // no concept of permission.
                Guid? customerId = user.IsInRole(Roles.Admin) ? null : CustomerId(user);

                var result = await handler.Handle(new GetOrdersQuery(customerId), cancellationToken);
                return result.ToHttpResult();
            })
            .WithName("GetOrders")
            .WithSummary("List orders")
            .WithDescription("Admins see all orders; everyone else sees only their own.");

        orders.MapGet("/{id:guid}", async (
                Guid id,
                ClaimsPrincipal user,
                IQueryHandler<GetOrderByIdQuery, OrderDetail> handler,
                CancellationToken cancellationToken) =>
            {
                Guid? customerId = user.IsInRole(Roles.Admin) ? null : CustomerId(user);

                var result = await handler.Handle(new GetOrderByIdQuery(id, customerId), cancellationToken);
                return result.ToHttpResult();
            })
            .WithName("GetOrderById")
            .WithSummary("Get one order")
            .WithDescription("Another customer's order returns 404, not 403, so its existence is not revealed.");

        return app;
    }

    /// <summary>
    /// The <c>sub</c> claim. JwtBearer maps it to NameIdentifier by default; the raw name
    /// is checked too, so this survives MapInboundClaims being turned off.
    /// </summary>
    private static Guid CustomerId(ClaimsPrincipal user)
    {
        var subject = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? user.FindFirst("sub")?.Value;

        return Guid.TryParse(subject, out var id) ? id : Guid.Empty;
    }
}
