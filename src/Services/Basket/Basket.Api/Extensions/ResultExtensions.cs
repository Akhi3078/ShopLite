using Basket.Application.Common;

namespace Basket.Api.Extensions;

/// <summary>
/// The single place where a use-case outcome becomes an HTTP response.
/// </summary>
/// <remarks>
/// Same shape as Catalog's mapper, with one extra case. <see cref="ErrorType.Unavailable"/>
/// becomes 503 rather than 500, because the distinction is the difference between "this
/// service is broken" and "a service it needs is not answering, try again shortly" — and
/// 503 is the status a client, gateway or load balancer can act on.
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

        ErrorType.NotFound => TypedResults.Problem(
            title: error.Code, detail: error.Message, statusCode: StatusCodes.Status404NotFound),

        ErrorType.Conflict => TypedResults.Problem(
            title: error.Code, detail: error.Message, statusCode: StatusCodes.Status409Conflict),

        ErrorType.Unavailable => TypedResults.Problem(
            title: error.Code, detail: error.Message, statusCode: StatusCodes.Status503ServiceUnavailable),

        _ => TypedResults.Problem(statusCode: StatusCodes.Status500InternalServerError)
    };
}
