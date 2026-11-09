namespace Basket.Application.Abstractions;

/// <summary>
/// Who is making the current request.
/// </summary>
/// <remarks>
/// Implemented in the Api layer, the only project with an HttpContext to read. Expressing
/// it as a port is what keeps ASP.NET Core out of Application and Infrastructure: the
/// alternative, injecting IHttpContextAccessor into the HTTP client, would drag a
/// FrameworkReference into class libraries that have no business with one.
/// </remarks>
public interface ICallerContext
{
    /// <summary>The <c>sub</c> claim: which customer's basket this request concerns.</summary>
    Guid CustomerId { get; }

    /// <summary>
    /// The caller's email, copied into <c>BasketCheckedOut</c> so Ordering can record who
    /// the order belongs to without calling Identity to ask.
    /// </summary>
    /// <remarks>
    /// Taken from the token rather than looked up, which is the same trade the roles make:
    /// no network hop, and it keeps working when Identity is down, at the cost of being as
    /// current as the token. For an order that is arguably correct anyway — it records the
    /// address the customer checked out with.
    /// </remarks>
    string Email { get; }

    /// <summary>
    /// The caller's raw bearer token, forwarded to Catalog because its endpoints require
    /// authentication. A real deployment would use the client-credentials flow instead;
    /// see ADR 0006 for why this shortcut is recorded rather than hidden.
    /// </summary>
    string? AccessToken { get; }
}
