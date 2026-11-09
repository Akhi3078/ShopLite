namespace Ordering.Application.Common;

/// <summary>
/// How a use case failed, expressed without reference to HTTP.
/// </summary>
/// <remarks>
/// A copy, as in Basket and Catalog, rather than something shared. Two services sharing a
/// mutable contract is how a distributed system quietly becomes a single deployable: a
/// change to the shared enum would force both to release together. The only shared project
/// is ShopLite.Contracts, and it holds messages — never this.
/// </remarks>
public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,

    /// <summary>
    /// A service this one depends on could not be reached, and no fallback covered it.
    /// Present but unused so far: Ordering reaches no one synchronously, which is the
    /// whole point of consuming a message instead of calling an API.
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
