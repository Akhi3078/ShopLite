using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ShopLite.ServiceDefaults;

/// <summary>
/// Allows the Angular dev server to call the APIs and gateway from its own origin.
/// </summary>
/// <remarks>
/// The gateway is the browser-facing API entry point. Keeping this policy on the APIs
/// also supports direct local API testing.
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
