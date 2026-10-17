namespace FixFlow.Application.Results;

public enum ErrorType
{
    /// <summary>The record doesn't exist or the caller may not see it (404, never 403).</summary>
    NotFound,

    /// <summary>A rule the request breaks, e.g. an unknown or foreign id in the body (422).</summary>
    Unprocessable,
}

/// <summary>
/// An expected error a handler returns instead of throwing. <see cref="FieldErrors"/> are keyed by the request's
/// property name (<c>nameof(command.TechnicianId)</c>); the API camelCases them to match the JSON.
/// Not just <c>Error</c>: that is a Visual Basic keyword (CA1716).
/// </summary>
public sealed record ResultError(
    ErrorType Type,
    string? Detail = null,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null);
