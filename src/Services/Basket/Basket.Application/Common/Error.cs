namespace Basket.Application.Common;

/// <summary>
/// How a use case failed, expressed without reference to HTTP.
/// </summary>
/// <remarks>
/// Deliberately a copy of Catalog's version rather than a shared package. Two services
/// sharing a mutable contract is how a distributed system quietly becomes a single
/// deployable: a change to the shared enum would force both to release together. Only
/// genuinely cross-cutting host concerns live in ShopLite.ServiceDefaults.
/// </remarks>
public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,

    /// <summary>
    /// A service this one depends on could not be reached, and no fallback covered it.
    /// Absent from Catalog's copy because Catalog calls no one, which is rather the point.
    /// </summary>
    Unavailable
}

public sealed record Error(ErrorType Type, string Code, string Message)
{
    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; init; }

    public static Error NotFound(string code, string message) =>
        new(ErrorType.NotFound, code, message);

    public static Error Conflict(string code, string message) =>
        new(ErrorType.Conflict, code, message);

    public static Error Unavailable(string code, string message) =>
        new(ErrorType.Unavailable, code, message);

    public static Error Validation(IReadOnlyDictionary<string, string[]> errors) =>
        new(ErrorType.Validation, "Validation", "One or more validation errors occurred.")
        {
            ValidationErrors = errors
        };
}
