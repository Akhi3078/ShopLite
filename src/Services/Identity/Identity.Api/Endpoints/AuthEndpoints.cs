using Identity.Api.Persistence;
using Identity.Api.Services;
using Microsoft.AspNetCore.Identity;
using ShopLite.ServiceDefaults;
using System.Security.Claims;

namespace Identity.Api.Endpoints;

public static class AuthEndpoints
{
    public sealed record RegisterRequest(string Email, string Password, string DisplayName);

    public sealed record LoginRequest(string Email, string Password);

    public sealed record AuthResponse(
        string Token, DateTimeOffset ExpiresAt, string Email, string DisplayName, string[] Roles);

    public sealed record CurrentUserResponse(string Email, string DisplayName, string[] Roles);

    public static IEndpointRouteBuilder MapAuth(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/auth").WithTags("Auth");

        auth.MapPost("/register", async (
                RegisterRequest request,
                UserManager<ApplicationUser> users,
                TokenService tokens) =>
            {
                var user = new ApplicationUser
                {
                    Id = Guid.CreateVersion7(),
                    UserName = request.Email,
                    Email = request.Email,
                    DisplayName = request.DisplayName
                };

                var created = await users.CreateAsync(user, request.Password);

                if (!created.Succeeded)
                {
                    return Results.ValidationProblem(ToProblem(created));
                }

                // Self-registration always yields a Customer. Elevating to Manager or
                // Admin is an administrative action, never something a signup form grants.
                await users.AddToRoleAsync(user, Roles.Customer);

                var roles = await users.GetRolesAsync(user);
                var token = tokens.Create(user, roles);

                return Results.Ok(new AuthResponse(
                    token.Token, token.ExpiresAt, user.Email!, user.DisplayName, [.. roles]));
            })
            .WithSummary("Register a new customer account");

        auth.MapPost("/login", async (
                LoginRequest request,
                UserManager<ApplicationUser> users,
                TokenService tokens) =>
            {
                var user = await users.FindByEmailAsync(request.Email);

                // Deliberately the same response whether the email is unknown or the
                // password is wrong. Distinguishing them tells an attacker which emails
                // are registered.
                if (user is null || !await users.CheckPasswordAsync(user, request.Password))
                {
                    return Results.Problem(
                        title: "Invalid credentials",
                        detail: "Email or password is incorrect.",
                        statusCode: StatusCodes.Status401Unauthorized);
                }

                var roles = await users.GetRolesAsync(user);
                var token = tokens.Create(user, roles);

                return Results.Ok(new AuthResponse(
                    token.Token, token.ExpiresAt, user.Email!, user.DisplayName, [.. roles]));
            })
            .WithSummary("Sign in and receive a JWT");

        auth.MapGet("/me", (ClaimsPrincipal principal) =>
            {
                var roles = principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray();

                return Results.Ok(new CurrentUserResponse(
                    principal.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty,
                    principal.FindFirst("displayName")?.Value ?? string.Empty,
                    roles));
            })
            .RequireAuthorization(Policies.Authenticated)
            .WithSummary("Who am I, according to my token");

        return app;
    }

    private static Dictionary<string, string[]> ToProblem(IdentityResult result) =>
        result.Errors
            .GroupBy(error => error.Code)
            .ToDictionary(group => group.Key, group => group.Select(e => e.Description).ToArray());
}
