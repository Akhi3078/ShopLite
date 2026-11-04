using Microsoft.AspNetCore.Identity;

namespace Identity.Api.Persistence;

/// <summary>
/// ASP.NET Core Identity's user, extended with a display name.
/// </summary>
/// <remarks>
/// Keyed by Guid rather than the default string, so the primary key is a real
/// uniqueidentifier in SQL Server instead of nvarchar(450).
/// </remarks>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole()
    {
    }

    public ApplicationRole(string name) : base(name)
    {
    }
}
