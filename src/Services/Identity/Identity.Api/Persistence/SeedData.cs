using Microsoft.AspNetCore.Identity;
using ShopLite.ServiceDefaults;

namespace Identity.Api.Persistence;

/// <summary>
/// Creates the three roles and one of each kind of user, so the UI can be exercised
/// without registering accounts by hand. Development only.
/// </summary>
public static class SeedData
{
    private const string DefaultPassword = "Passw0rd!";

    public static async Task SeedIdentityAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new ApplicationRole(role) { Id = Guid.CreateVersion7() });
            }
        }

        await EnsureUser(userManager, "admin@shoplite.local", "Ada Admin", Roles.Admin);
        await EnsureUser(userManager, "manager@shoplite.local", "Mo Manager", Roles.Manager);
        await EnsureUser(userManager, "customer@shoplite.local", "Cal Customer", Roles.Customer);
    }

    private static async Task EnsureUser(
        UserManager<ApplicationUser> userManager, string email, string displayName, string role)
    {
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            DisplayName = displayName,
            EmailConfirmed = true
        };

        var created = await userManager.CreateAsync(user, DefaultPassword);

        if (created.Succeeded)
        {
            await userManager.AddToRoleAsync(user, role);
        }
    }
}
