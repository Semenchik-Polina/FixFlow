using System.Text.Json;
using FixFlow.API.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace FixFlow.API.IntegrationTests.Conventions;

/// <summary>
/// The test host's document comes from the same <c>AddFixFlowOpenApi</c> setup as the one <c>dotnet build</c> writes,
/// and it includes the test-only endpoints' DTOs.
/// </summary>
public class OpenApiDocumentTests(FixFlowApiFactory api)
{
    [Fact]
    public async Task Document_NodaTimeTypes_AreInlineFormattedStrings()
    {
        var sample = (await ReadDocumentAsync()).GetProperty("components").GetProperty("schemas").GetProperty("Sample");
        var properties = sample.GetProperty("properties");

        AssertString(properties.GetProperty("at"), "date-time", nullable: false);
        AssertString(properties.GetProperty("on"), "date", nullable: false);
        AssertString(properties.GetProperty("notSet"), "date-time", nullable: true);
    }

    [Fact]
    public async Task Document_Enums_AreStrings()
    {
        var schemas = (await ReadDocumentAsync()).GetProperty("components").GetProperty("schemas");

        var status = schemas.GetProperty("SampleStatus");

        Assert.Equal("string", status.GetProperty("type").GetString());
        Assert.Equal(
            ["New", "InProgress", "OnHold"],
            status.GetProperty("enum").EnumerateArray().Select(value => value.GetString()));
    }

    [Fact]
    public async Task Document_Numbers_AreJsonNumbersOnly()
    {
        var document = await ReadDocumentAsync();

        // The paged result's counts: a plain integer, not "integer or numeric string"
        var paged = document.GetProperty("components").GetProperty("schemas").EnumerateObject()
            .Single(schema => schema.Name.StartsWith("PagedResultOf", StringComparison.Ordinal)).Value;
        Assert.Equal(["integer"], TypesOf(paged.GetProperty("properties").GetProperty("totalCount")));

        // Anywhere in the document, including query parameters: the generated client would type them number | string
        Assert.Empty(NumericSchemasAcceptingStrings(document, "#"));
    }

    private static IEnumerable<string> NumericSchemasAcceptingStrings(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            return element.EnumerateArray().SelectMany((item, index) => NumericSchemasAcceptingStrings(item, $"{path}/{index}"));
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        // A schema's "type" is a string or an array; under "properties" it can also be a property named "type"
        string?[] types = element.TryGetProperty("type", out var type)
            && type.ValueKind is JsonValueKind.String or JsonValueKind.Array
                ? TypesOf(element)
                : [];
        IEnumerable<string> here = types.Contains("string") && (types.Contains("integer") || types.Contains("number"))
            ? [path]
            : [];

        return here.Concat(element.EnumerateObject()
            .SelectMany(property => NumericSchemasAcceptingStrings(property.Value, $"{path}/{property.Name}")));
    }

    private static string?[] TypesOf(JsonElement schema)
    {
        var type = schema.GetProperty("type");
        return type.ValueKind == JsonValueKind.Array
            ? [.. type.EnumerateArray().Select(value => value.GetString())]
            : [type.GetString()];
    }

    private static void AssertString(JsonElement schema, string format, bool nullable)
    {
        Assert.False(schema.TryGetProperty("$ref", out _), "NodaTime types are inlined");
        Assert.Equal(format, schema.GetProperty("format").GetString());
        Assert.Equal(nullable ? ["null", "string"] : ["string"], TypesOf(schema).Order(StringComparer.Ordinal));
    }

    private async Task<JsonElement> ReadDocumentAsync()
    {
        var provider = api.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1");
        var document = await provider.GetOpenApiDocumentAsync(TestContext.Current.CancellationToken);

        await using var writer = new StringWriter();
        document.SerializeAsV31(new OpenApiJsonWriter(writer));
        return JsonDocument.Parse(writer.ToString()).RootElement.Clone();
    }
}
