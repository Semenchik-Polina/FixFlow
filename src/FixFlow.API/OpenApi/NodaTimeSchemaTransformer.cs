using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using NodaTime;

namespace FixFlow.API.OpenApi;

/// <summary>
/// NodaTime values are serialized as ISO-8601 strings (<see cref="Json.JsonExtensions"/>), but their custom converters
/// leave the generated schema empty. This describes them as formatted strings, inline, so the client gets
/// <c>string</c> types.
/// </summary>
internal sealed class NodaTimeSchemaTransformer : IOpenApiSchemaTransformer
{
    /// <summary>
    /// Formats from the OpenAPI format registry. Local values have no offset: the property's time zone places them
    /// (§11). A time zone is written as its IANA id, e.g. <c>Europe/Warsaw</c>, which no format describes.
    /// </summary>
    private static readonly Dictionary<Type, string?> _formats = new()
    {
        [typeof(Instant)] = "date-time",
        [typeof(LocalDate)] = "date",
        [typeof(LocalTime)] = "time-local",
        [typeof(LocalDateTime)] = "date-time-local",
        [typeof(DateTimeZone)] = null,
    };

    public static bool Handles(Type type) => _formats.ContainsKey(Nullable.GetUnderlyingType(type) ?? type);

    public Task TransformAsync(
        OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;
        var underlying = Nullable.GetUnderlyingType(type);
        if (!_formats.TryGetValue(underlying ?? type, out var format))
        {
            return Task.CompletedTask;
        }

        schema.Type = underlying is null ? JsonSchemaType.String : JsonSchemaType.String | JsonSchemaType.Null;
        schema.Format = format;
        schema.Properties?.Clear();
        schema.Required?.Clear();

        return Task.CompletedTask;
    }
}
