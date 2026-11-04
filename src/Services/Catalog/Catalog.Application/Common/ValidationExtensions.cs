using FluentValidation;

namespace Catalog.Application.Common;

internal static class ValidationExtensions
{
    /// <summary>
    /// Runs a validator and returns an <see cref="Error"/> if it failed, or null if it passed.
    /// </summary>
    /// <remarks>
    /// Called explicitly at the top of each handler rather than hidden in a pipeline
    /// behaviour. More repetition, but the failure path is visible in the handler that
    /// owns it, and there is no DI decorator to reason about. A Wolverine middleware
    /// would be the natural place to centralise this later.
    /// </remarks>
    public static async Task<Error?> CheckAsync<T>(
        this IValidator<T> validator, T instance, CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(instance, cancellationToken);

        if (result.IsValid)
        {
            return null;
        }

        var errors = result.Errors
            .GroupBy(failure => failure.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).ToArray());

        return Error.Validation(errors);
    }
}
