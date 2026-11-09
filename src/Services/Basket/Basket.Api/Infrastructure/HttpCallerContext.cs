using System.Security.Claims;
using Basket.Application.Abstractions;

namespace Basket.Api.Infrastructure;

/// <summary>
/// Supplies the current customer and their bearer token from the HTTP request.
/// </summary>
/// <remarks>
/// This is the whole reason <see cref="ICallerContext"/> exists: the Api layer is the only
/// one that knows what an HttpContext is, so the knowledge stops here instead of leaking
/// into the use cases or the HTTP client.
/// </remarks>
internal sealed class HttpCallerContext(IHttpContextAccessor accessor) : ICallerContext
{
    public Guid CustomerId
    {
        get
        {
            var user = accessor.HttpContext?.User;

            // JwtBearer maps the inbound "sub" claim to NameIdentifier by default. The
            // raw name is checked too, so this keeps working if MapInboundClaims is ever
            // turned off — a change that otherwise silently empties every basket, because
            // a different customer id means a different basket.
            var subject = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? user?.FindFirst("sub")?.Value;

            return Guid.TryParse(subject, out var customerId) ? customerId : Guid.Empty;
        }
    }

    public string Email
    {
        get
        {
            var user = accessor.HttpContext?.User;

            // Identity's TokenService writes "email"; ClaimTypes.Email is checked too in
            // case claim mapping is ever turned on, and Name last because it holds the
            // email in this system.
            return user?.FindFirst("email")?.Value
                   ?? user?.FindFirst(ClaimTypes.Email)?.Value
                   ?? user?.FindFirst(ClaimTypes.Name)?.Value
                   ?? string.Empty;
        }
    }

    public string? AccessToken
    {
        get
        {
            var header = accessor.HttpContext?.Request.Headers.Authorization.ToString();

            return string.IsNullOrWhiteSpace(header)
                   || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? null
                : header["Bearer ".Length..].Trim();
        }
    }
}
