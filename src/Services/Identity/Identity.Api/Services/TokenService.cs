using Identity.Api.Persistence;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Identity.Api.Services;

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>
/// Mints the JWTs that every other service validates.
/// </summary>
/// <remarks>
/// Roles go into the token as claims. That is the whole mechanism behind role-based
/// access: Catalog never calls Identity to ask what a user may do — it reads the roles
/// out of the signed token it was given. One fewer network hop, one fewer service to be
/// down, and the cost is that a role change only takes effect when the token is reissued.
/// </remarks>
public sealed class TokenService(IConfiguration configuration)
{
    public AccessToken Create(ApplicationUser user, IEnumerable<string> roles)
    {
        var jwt = configuration.GetSection("Jwt");

        var key = jwt["Key"]
            ?? throw new InvalidOperationException("Jwt:Key is not configured.");

        var expiryMinutes = int.TryParse(jwt["ExpiryMinutes"], out var minutes) ? minutes : 60;
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Subject, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new("displayName", user.DisplayName)
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    private static class JwtRegisteredClaimNames
    {
        public const string Subject = "sub";
        public const string Email = "email";
    }
}
