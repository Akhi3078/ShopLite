using Ordering.Api.Endpoints;
using Ordering.Application;
using Ordering.Infrastructure;
using Scalar.AspNetCore;
using ShopLite.ServiceDefaults;
using Wolverine;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddShopLiteAuthentication();
builder.AddShopLiteCors();

builder.AddOrderingInfrastructure();
builder.Services.AddOrderingApplication();

// ── Messaging ────────────────────────────────────────────────────────────────────────
builder.UseWolverine(options =>
{
    options.UseShopLiteMessaging(builder);

    // Subscribe. On Service Bus this is a topic subscription; on RabbitMQ a queue bound
    // to a fanout exchange. Either way it is publish/subscribe: Basket publishes once and
    // each consumer gets its own copy with its own cursor.
    options.ListenForBasketCheckedOut(builder);
});

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.MapDefaultEndpoints();
app.UseShopLiteCors();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapOrders();

await app.RunAsync();
