using FixFlow.API.Errors;
using FixFlow.Application.Results;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FixFlow.API.Endpoints;

/// <summary>
/// Maps a handler's <see cref="Result"/> to <c>TypedResults</c>. Errors become Problem Details: NotFound → 404 with
/// no detail, Unprocessable → 422 with the detail or the field errors. Endpoints still declare those responses with
/// <c>ProducesProblem</c>, which the union type alone doesn't describe.
/// </summary>
public static class ResultExtensions
{
    public static Results<Ok<T>, ProblemHttpResult> ToOk<T>(this Result<T> result) =>
        result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();

    /// <summary>A 201 with the new resource and no <c>Location</c>, for a resource without a GET endpoint yet.</summary>
    public static Results<Created<T>, ProblemHttpResult> ToCreated<T>(this Result<T> result) =>
        result.IsSuccess ? TypedResults.Created((string?)null, result.Value) : result.Error.ToProblem();

    public static Results<NoContent, ProblemHttpResult> ToNoContent(this Result result) =>
        result.IsSuccess ? TypedResults.NoContent() : result.Error.ToProblem();

    /// <summary>
    /// A 202 with no <c>Location</c>, for a request whose outcome the answer doesn't tell, such as whether a reset email
    /// went out. There is nothing to poll.
    /// </summary>
    public static Results<Accepted, ProblemHttpResult> ToAccepted(this Result result) =>
        result.IsSuccess ? TypedResults.Accepted((string?)null) : result.Error.ToProblem();

    public static ProblemHttpResult ToProblem(this ResultError error) => error switch
    {
        { Type: ErrorType.NotFound } => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound),
        { FieldErrors: { } fieldErrors } => TypedResults.Problem(
            new HttpValidationProblemDetails(FieldNames.ToCamelCase(fieldErrors))
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Detail = error.Detail,
            }),
        _ => TypedResults.Problem(error.Detail, statusCode: StatusCodes.Status422UnprocessableEntity),
    };
}
