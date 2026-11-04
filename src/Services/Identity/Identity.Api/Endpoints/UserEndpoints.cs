using Identity.Api.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShopLite.ServiceDefaults;

namespace Identity.Api.Endpoints;

/// <summary>
/// User administration. Every endpoint here is Admin-only.
/// </summary>
public static class UserEndpoints
{
    public sealed record UserListItem(Guid Id, string Email, string DisplayName, string[] Roles);

    public sealed record SetRolesRequest(string[] Roles);

    public static IEndpointRouteBuilder MapUsers(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/users")
            .WithTags("Users")
            .RequireAuthorization(Policies.ManageUsers);

        users.MapGet("/", async (ShopLiteIdentityDbContext db, CancellationToken cancellationToken) =>
            {
                // One query with a join rather than UserManager.GetRolesAsync per user,
                // which would be N+1 against the database.
                var result = await db.Users
                    .AsNoTracking()
                    .OrderBy(user => user.Email)
                    .Select(user => new UserListItem(
                        user.Id,
                        user.Email ?? string.Empty,
                        user.DisplayName,
                        db.UserRoles
                            .Where(userRole => userRole.UserId == user.Id)
                            .Join(db.Roles, ur => ur.RoleId, role => role.Id, (ur, role) => role.Name!)
                            .ToArray()))
                    .ToListAsync(cancellationToken);

                return Results.Ok(result);
            })
            .WithSummary("List all users and their roles");

        users.MapGet("/roles", () => Results.Ok(Roles.All))
            .WithSummary("The roles that exist");

        users.MapPut("/{id:guid}/roles", async (
                Guid id,
                SetRolesRequest request,
                UserManager<ApplicationUser> userManager) =>
            {
                var user = await userManager.FindByIdAsync(id.ToString());

                if (user is null)
                {
                    return Results.NotFound();
                }

                var unknown = request.Roles.Except(Roles.All).ToArray();

                if (unknown.Length > 0)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["roles"] = [$"Unknown role(s): {string.Join(", ", unknown)}."]
                    });
                }

                // Replace rather than add, so the request body is the complete desired
                // state. An "add role" endpoint makes removing one an awkward second call.
                var current = await userManager.GetRolesAsync(user);
                await userManager.RemoveFromRolesAsync(user, current.Except(request.Roles));
                await userManager.AddToRolesAsync(user, request.Roles.Except(current));

                return Results.NoContent();
            })
            .WithSummary("Replace a user's roles");

        return app;
    }
}
