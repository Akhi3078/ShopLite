namespace Catalog.Application.Common;

/// <summary>
/// How a use case failed, expressed without reference to HTTP. The Api layer decides
/// which status code each <see cref="ErrorType"/> maps to — the Application layer must
/// not know that "not found" means 404, or it could not be reused by a message consumer
/// or a background job.
/// </summary>
public enum ErrorType
{
    Validation,
    NotFound,
    Conflict
}

public sealed record Error(ErrorType Type, string Code, string Message)
{
    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; init; }

    public static Error NotFound(string code, string message) =>
        new(ErrorType.NotFound, code, message);

    public static Error Conflict(string code, string message) =>
        new(ErrorType.Conflict, code, message);

    public static Error Validation(IReadOnlyDictionary<string, string[]> errors) =>
        new(ErrorType.Validation, "Validation", "One or more validation errors occurred.")
        {
            ValidationErrors = errors
        };
}
