using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace ShopLite.ServiceDefaults;

/// <summary>
/// ShopLite's role names, in one place so a typo cannot silently grant or deny access.
/// </summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Customer = "Customer";

    public static readonly string[] All = [Admin, Manager, Customer];
}

/// <summary>
/// Authorization policy names. Endpoints reference these rather than listing roles
/// inline, so changing who may edit products is a one-line change here instead of a
/// search across every service.
/// </summary>
public static class Policies
{
    /// <summary>Any signed-in user.</summary>
    public const string Authenticated = "Authenticated";

    /// <summary>May create, update and delete catalog data. Admin or Manager.</summary>
    public const string ManageCatalog = "ManageCatalog";

    /// <summary>May administer users and roles. Admin only.</summary>
    public const string ManageUsers = "ManageUsers";
}

public static class AuthenticationExtensions
{
    /// <summary>
    /// Validates JWTs issued by the Identity service. Every API calls this, so the
    /// validation rules cannot drift between services.
    /// </summary>
    /// <remarks>
    /// The signing key is symmetric (HMAC), which means every service holds the secret
    /// that can also *mint* tokens. That is acceptable for local learning and wrong for
    /// production, where the issuer signs with a private key and services validate with
    /// the public half fetched from a JWKS endpoint. Noted in ADR 0005.
    /// </remarks>
    public static TBuilder AddShopLiteAuthentication<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var jwt = builder.Configuration.GetSection("Jwt");

        var key = jwt["Key"]
            ?? throw new InvalidOperationException(
                "Jwt:Key is not configured. The AppHost supplies it to every service.");

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt["Issuer"],

                    ValidateAudience = true,
                    ValidAudience = jwt["Audience"],

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),

                    // Tokens expire when they say they expire. The default allows five
                    // minutes of clock skew, which hides expiry bugs during development.
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(Policies.Authenticated, policy => policy.RequireAuthenticatedUser())
            .AddPolicy(Policies.ManageCatalog, policy => policy.RequireRole(Roles.Admin, Roles.Manager))
            .AddPolicy(Policies.ManageUsers, policy => policy.RequireRole(Roles.Admin));

        return builder;
    }
}
