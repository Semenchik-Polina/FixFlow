using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FixFlow.API.IntegrationTests.Fixtures;

namespace FixFlow.API.IntegrationTests.Conventions;

public class JsonTests(FixFlowApiFactory api)
{
    [Fact]
    public async Task Get_Sample_WritesCamelCaseIsoTimesAndEnumNames()
    {
        using var client = api.CreateApiClient();

        var json = await client.GetStringAsync(
            new Uri($"{ConventionTestEndpoints.Path}/samples/current", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // The shared clock: FixFlowApiFactory.SharedNow, 10:00 in Warsaw (UTC+1 in winter)
        AssertJsonEqual(
            """
            {"name":"Current","at":"2026-01-14T09:00:00Z","on":"2026-01-14","opensAt":"10:00:00","visitAt":"2026-01-14T10:00:00","timeZone":"Europe/Warsaw","notSet":null,"status":"InProgress","tags":["boiler"]}
            """,
            json);
    }

    [Fact]
    public async Task Post_Sample_ReadsIsoTimesAndEnumNames()
    {
        const string Sample =
            """
            {"name":"Echo","at":"2026-03-29T00:59:59.5Z","on":"2026-03-29","opensAt":"08:30:00","visitAt":"2026-03-29T09:15:00","timeZone":"Europe/Warsaw","notSet":"2026-03-29T01:00:00Z","status":"OnHold","tags":["a","b"]}
            """;
        using var client = api.CreateApiClient();
        using var content = new StringContent(Sample, Encoding.UTF8, "application/json");
        var cancellationToken = TestContext.Current.CancellationToken;

        using var response = await client.PostAsync(
            new Uri($"{ConventionTestEndpoints.Path}/samples", UriKind.Relative), content, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertJsonEqual(Sample, await response.Content.ReadAsStringAsync(cancellationToken));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("\"1\"")]
    public async Task Post_Sample_NumericEnumValue_Returns400(string status)
    {
        // Enums are names only: a number would bind to any value, even an undefined one
        var sample =
            $$"""
            {"name":"Echo","at":"2026-03-29T00:00:00Z","on":"2026-03-29","opensAt":"08:30:00","visitAt":"2026-03-29T09:15:00","timeZone":"Europe/Warsaw","status":{{status}},"tags":[]}
            """;
        using var client = api.CreateApiClient();
        using var content = new StringContent(sample, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(
            new Uri($"{ConventionTestEndpoints.Path}/samples", UriKind.Relative),
            content,
            TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.BadRequest);
    }

    private static void AssertJsonEqual(string expected, string actual) =>
        Assert.True(
            JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(actual)),
            $"Expected {expected}{Environment.NewLine}Actual   {JsonSerializer.Serialize(JsonNode.Parse(actual))}");
}
