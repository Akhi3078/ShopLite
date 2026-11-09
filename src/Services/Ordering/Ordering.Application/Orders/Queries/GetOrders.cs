using Microsoft.EntityFrameworkCore;
using Ordering.Application.Abstractions;
using Ordering.Application.Common;
using Ordering.Domain.Entities;

namespace Ordering.Application.Orders.Queries;

/// <param name="CustomerId">
/// Null means "every customer", which only an Admin may ask for. The decision is the Api
/// layer's; this query just honours whichever it was given.
/// </param>
public sealed record GetOrdersQuery(Guid? CustomerId);

public sealed record OrderSummary(
    Guid Id,
    Guid CustomerId,
    string CustomerEmail,
    string Status,
    decimal Total,
    int LineCount,
    DateTimeOffset PlacedAt);

public sealed class GetOrdersHandler(IOrderingDbContext db)
    : IQueryHandler<GetOrdersQuery, IReadOnlyList<OrderSummary>>
{
    public async Task<Result<IReadOnlyList<OrderSummary>>> Handle(
        GetOrdersQuery query, CancellationToken cancellationToken)
    {
        var orders = db.Orders.AsNoTracking();

        if (query.CustomerId is { } customerId)
        {
            orders = orders.Where(order => order.CustomerId == customerId);
        }

        // Projected in the database, so the line count comes back as a COUNT rather than
        // by loading every line and counting in memory.
        var results = await orders
            .OrderByDescending(order => order.PlacedAt)
            .Select(order => new OrderSummary(
                order.Id,
                order.CustomerId,
                order.CustomerEmail,
                order.Status.ToString(),
                order.Total,
                order.Lines.Count,
                order.PlacedAt))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<OrderSummary>>.Success(results);
    }
}

public sealed record GetOrderByIdQuery(Guid Id, Guid? CustomerId);

public sealed record OrderDetail(
    Guid Id,
    Guid CustomerId,
    string CustomerEmail,
    string Status,
    decimal Total,
    DateTimeOffset PlacedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<OrderDetailLine> Lines);

public sealed record OrderDetailLine(
    Guid ProductId, string ProductName, decimal UnitPrice, int Quantity, decimal LineTotal);

public sealed class GetOrderByIdHandler(IOrderingDbContext db)
    : IQueryHandler<GetOrderByIdQuery, OrderDetail>
{
    public async Task<Result<OrderDetail>> Handle(
        GetOrderByIdQuery query, CancellationToken cancellationToken)
    {
        var order = await db.Orders
            .AsNoTracking()
            .Include(candidate => candidate.Lines)
            .FirstOrDefaultAsync(candidate => candidate.Id == query.Id, cancellationToken);

        // A customer asking for someone else's order gets NotFound, not Forbidden.
        // Telling them "that exists but is not yours" leaks that it exists at all, which
        // for an order id is enough to confirm another customer's activity.
        if (order is null || (query.CustomerId is { } customerId && order.CustomerId != customerId))
        {
            return Result<OrderDetail>.Fail(Error.NotFound(
                "Order.NotFound", $"No order with id {query.Id}."));
        }

        return Result<OrderDetail>.Success(new OrderDetail(
            order.Id,
            order.CustomerId,
            order.CustomerEmail,
            order.Status.ToString(),
            order.Total,
            order.PlacedAt,
            order.UpdatedAt,
            [.. order.Lines.Select(line => new OrderDetailLine(
                line.ProductId, line.ProductName, line.UnitPrice, line.Quantity, line.LineTotal))]));
    }
}
