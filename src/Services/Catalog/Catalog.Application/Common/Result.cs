namespace Catalog.Application.Common;

/// <summary>
/// The outcome of a use case that returns no value.
/// </summary>
/// <remarks>
/// Expected failures are returned, not thrown. Exceptions stay for genuinely exceptional
/// conditions, so a missing product does not cost a stack trace and the compiler keeps
/// the failure path visible at every call site.
/// </remarks>
public readonly record struct Result
{
    private Result(bool isSuccess, Error? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public Error? Error { get; }

    public static Result Success() => new(true, null);

    public static Result Fail(Error error) => new(false, error);
}

/// <summary>The outcome of a use case that returns a value.</summary>
public readonly record struct Result<T>
{
    private Result(bool isSuccess, T? value, Error? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public bool IsSuccess { get; }

    public T? Value { get; }

    public Error? Error { get; }

    public static Result<T> Success(T value) => new(true, value, null);

    public static Result<T> Fail(Error error) => new(false, default, error);
}
