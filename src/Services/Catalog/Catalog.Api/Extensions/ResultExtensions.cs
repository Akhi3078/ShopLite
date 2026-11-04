using Catalog.Application.Common;

namespace Catalog.Api.Extensions;

/// <summary>
/// The single place where a use-case outcome becomes an HTTP response.
/// </summary>
/// <remarks>
/// This mapping is the reason the Application layer never references ASP.NET Core.
/// Keeping it in one file means the service's error-to-status-code policy is auditable
/// in about thirty lines instead of being scattered across every endpoint.
/// </remarks>
public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result) =>
        result.IsSuccess
            ? TypedResults.Ok(result.Value)
            : ToProblem(result.Error!);

    public static IResult ToHttpResult(this Result result) =>
        result.IsSuccess
            ? TypedResults.NoContent()
            : ToProblem(result.Error!);

    private static IResult ToProblem(Error error) => error.Type switch
    {
        ErrorType.Validation => TypedResults.ValidationProblem(
            error.ValidationErrors!.ToDictionary(entry => entry.Key, entry => entry.Value)),

        ErrorType.NotFound => TypedResults.NotFound(),

        ErrorType.Conflict => TypedResults.Problem(
            title: error.Code, detail: error.Message, statusCode: StatusCodes.Status409Conflict),

        _ => TypedResults.Problem(statusCode: StatusCodes.Status500InternalServerError)
    };
}
