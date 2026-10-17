using System.Text.Json.Serialization;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace FixFlow.API.Json;

public static class JsonExtensions
{
    /// <summary>
    /// camelCase (the web default), enums as their names, and NodaTime as ISO-8601 strings: an <c>Instant</c> as
    /// <c>2026-10-07T13:00:00Z</c>, a <c>LocalDate</c> as <c>2026-10-07</c> (ARCHITECTURE §11, §15).
    /// The OpenAPI document reads the same options.
    /// </summary>
    public static IServiceCollection AddFixFlowJson(this IServiceCollection services) =>
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            options.SerializerOptions.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
        });
}
