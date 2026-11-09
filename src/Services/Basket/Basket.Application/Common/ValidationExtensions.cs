using FluentValidation;

namespace Basket.Application.Common;

internal static class ValidationExtensions
{
    /// <summary>
    /// Runs a validator and returns an <see cref="Error"/> if it failed, or null if it
    /// passed. Called explicitly at the top of each handler, as in Catalog.
    /// </summary>
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
