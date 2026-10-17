using FixFlow.Application.Errors;
using FixFlow.Domain;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FixFlow.API.Errors;

/// <summary>
/// Turns the expected exceptions into Problem Details: validation → 400 with field errors, <see cref="DomainException"/>
/// → 422 with its message, <see cref="ConcurrencyConflictException"/> → 409. Anything else is left to the exception
/// handler middleware, which logs it and answers a generic 500. No response ever carries exception details (S15).
/// </summary>
internal sealed class ProblemExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails? problem = exception switch
        {
            ValidationException validation => new HttpValidationProblemDetails(FieldNames.FromFailures(validation.Errors))
            {
                Status = StatusCodes.Status400BadRequest,
            },
            DomainException domain => new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Detail = domain.Message,
            },
            ConcurrencyConflictException => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Detail = "The record was changed by someone else. Reload it and try again.",
            },
            // Malformed input the framework rejected while binding or reading the body
            BadHttpRequestException badRequest => new ProblemDetails { Status = badRequest.StatusCode },
            _ => null,
        };

        if (problem is null)
        {
            return false;
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        // False only when the client accepts no JSON: the status code alone still answers it
        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
        });

        return true;
    }
}
