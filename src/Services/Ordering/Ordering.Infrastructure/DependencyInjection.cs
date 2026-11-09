using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ordering.Application.Abstractions;
using Ordering.Infrastructure.Persistence;

namespace Ordering.Infrastructure;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddOrderingInfrastructure(this IHostApplicationBuilder builder)
    {
        builder.AddSqlServerDbContext<OrderingDbContext>("orderingdb");

        // Registered type-to-type, NOT as a lambda — and that detail is load-bearing.
        //
        // Catalog and Basket map their port with
        // `AddScoped<IPort>(sp => sp.GetRequiredService<Context>())`, which is idiomatic
        // and fine for an HTTP-only service. Ordering cannot: Wolverine generates the code
        // that invokes message handlers and inspects the container to construct their
        // dependencies, and it cannot see through a lambda. Faced with an "opaque lambda
        // factory" it falls back to service location, which its default policy rejects —
        // throwing InvalidServiceLocationException *while handling a message*, so the
        // service reports itself healthy while every message retries and dead-letters.
        //
        // The trade is that resolving IOrderingDbContext constructs its own instance
        // rather than returning the one registered for OrderingDbContext. Harmless here
        // because nothing outside design-time tooling asks for the concrete type, so there
        // is still one context — and therefore one change tracker — per scope. Aspire's
        // configuration still applies, since it lives on DbContextOptions<OrderingDbContext>.
        builder.Services.AddScoped<IOrderingDbContext, OrderingDbContext>();

        return builder;
    }
}
