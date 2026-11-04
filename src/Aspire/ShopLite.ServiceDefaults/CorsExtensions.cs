using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ShopLite.ServiceDefaults;

/// <summary>
/// Lets the Angular dev server call these APIs from its own origin.
/// </summary>
/// <remarks>
/// Needed only because the browser talks to each API directly in development. Once the
/// YARP gateway lands in Phase 2 the UI will have a single same-origin entry point and
/// this can go away — which is one of the less obvious reasons gateways exist.
/// </remarks>
public static class CorsExtensions
{
    private const string PolicyName = "shoplite-web";

    public static TBuilder AddShopLiteCors<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        // Configured by the AppHost; falls back to the Angular CLI's default port.
        var origins = builder.Configuration["Cors:AllowedOrigins"]?
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? ["http://localhost:4200"];

        builder.Services.AddCors(options =>
            options.AddPolicy(PolicyName, policy => policy
                .WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod()));

        return builder;
    }

    public static WebApplication UseShopLiteCors(this WebApplication app)
    {
        app.UseCors(PolicyName);
        return app;
    }
}
