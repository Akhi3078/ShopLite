using Basket.Api.Endpoints;
using Basket.Api.Infrastructure;
using Basket.Application;
using Basket.Application.Abstractions;
using Basket.Infrastructure;
using Scalar.AspNetCore;
using Wolverine;
using ShopLite.Contracts;
using ShopLite.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Validates tokens; never issues them. Basket also forwards the caller's token on to
// Catalog, which is why it needs the same issuer and audience settings.
builder.AddShopLiteAuthentication();

// Basket sits behind the gateway in normal use, but keeps its own policy so it can be
// called directly while testing, as Catalog and Identity do.
builder.AddShopLiteCors();

// Redis, the resilient Catalog client, and the outbox publisher.
builder.AddBasketInfrastructure();
builder.Services.AddBasketApplication();

// ICallerContext is implemented here because this is the only layer with an HttpContext.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICallerContext, HttpCallerContext>();

// ── Messaging ────────────────────────────────────────────────────────────────────────
// Basket only publishes; it listens to nothing. Wolverine is still needed here because
// the outbox publisher uses IMessageBus to send what it drains.
builder.UseWolverine(options =>
{
    options.UseShopLiteMessaging(builder);

    // Where BasketCheckedOut goes. A routing rule, not a subscription: Basket sends to a
    // topic (or exchange) and never learns who reads it, which is what lets Ordering be
    // added, removed or duplicated without touching this line.
    options.PublishBasketCheckedOut<BasketCheckedOut>(builder);
});

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.MapDefaultEndpoints();

// CORS before authentication: a preflight OPTIONS carries no credentials, so it has to be
// answered before anything tries to authenticate it.
app.UseShopLiteCors();

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapBasket();

app.Run();
