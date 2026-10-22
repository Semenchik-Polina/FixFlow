using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FixFlow.API.IntegrationTests.Fixtures;

public static class ProblemDetailsAssertions
{
    /// <summary>
    /// Asserts the response is RFC 9457 Problem Details with <paramref name="expected"/> status, a title and a traceId,
    /// and no exception details (S15). Returns the body for further checks.
    /// </summary>
    public static async Task<JsonElement> ReadProblemAsync(this HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal((int)expected, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
        Assert.False(problem.TryGetProperty("exception", out _));
        Assert.False(problem.TryGetProperty("stackTrace", out _));
        return problem;
    }

    /// <summary>The <c>errors</c> member: field name as the JSON spells it → messages.</summary>
    public static Dictionary<string, string[]> FieldErrors(this JsonElement problem) =>
        problem.GetProperty("errors").Deserialize<Dictionary<string, string[]>>()
        ?? throw new InvalidOperationException("The problem has no field errors.");
}
