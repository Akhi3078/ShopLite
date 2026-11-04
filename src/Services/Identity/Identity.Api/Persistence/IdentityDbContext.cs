using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Persistence;

/// <summary>
/// Identity's exclusive database (ShopLite_Identity). The seven AspNetUsers/AspNetRoles
/// tables come from the base class; nothing here is hand-written.
/// </summary>
public sealed class ShopLiteIdentityDbContext(DbContextOptions<ShopLiteIdentityDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(user =>
        {
            user.Property(u => u.DisplayName).HasMaxLength(200).IsRequired();
        });
    }
}
