using System.Text.Json;
using FluentValidation.Results;

namespace FixFlow.API.Errors;

/// <summary>
/// Field error keys as the JSON names them: each segment of a C# property path goes through the serializer's
/// camelCase policy (<c>PageSize</c> → <c>pageSize</c>, <c>Tags[0]</c> → <c>tags[0]</c>,
/// <c>Address.Street</c> → <c>address.street</c>), so the frontend can map them back to form fields.
/// </summary>
internal static class FieldNames
{
    public static string ToCamelCase(string path) =>
        string.Join('.', path.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));

    public static Dictionary<string, string[]> ToCamelCase(IEnumerable<KeyValuePair<string, string[]>> errors) =>
        errors
            .GroupBy(error => ToCamelCase(error.Key), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.SelectMany(error => error.Value).ToArray(), StringComparer.Ordinal);

    public static Dictionary<string, string[]> FromFailures(IEnumerable<ValidationFailure> failures) =>
        failures
            .GroupBy(failure => ToCamelCase(failure.PropertyName), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).Distinct().ToArray(),
                StringComparer.Ordinal);
}
