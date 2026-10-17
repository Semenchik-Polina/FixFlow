using System.Net;
using System.Net.Http.Json;
using FixFlow.API.IntegrationTests.Fixtures;
using FixFlow.Domain;
using Serilog.Events;

namespace FixFlow.API.IntegrationTests.Conventions;

public class ProblemDetailsTests(FixFlowApiFactory api)
{
    private const string Path = ConventionTestEndpoints.Path;

    private static readonly string[] _emptyTag = [""];

    [Fact]
    public async Task Get_ResultNotFound_Returns404WithoutDetail()
    {
        using var client = api.CreateApiClient();

        using var response = await client.GetAsync(
            new Uri($"{Path}/samples/{Guid.CreateVersion7()}", UriKind.Relative), TestContext.Current.CancellationToken);

        var problem = await response.ReadProblemAsync(HttpStatusCode.NotFound);
        Assert.False(problem.TryGetProperty("detail", out _));
    }

    [Fact]
    public async Task Post_DomainException_Returns422WithMessage()
    {
        using var client = api.CreateApiClient();

        using var response = await client.PostAsync(
            new Uri($"{Path}/domain-rule", UriKind.Relative), null, TestContext.Current.CancellationToken);

        var problem = await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity);
        Assert.Equal(BreakDomainRule.Message, problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Post_DomainException_IsNotLoggedAsError()
    {
        using var client = api.CreateApiClient();

        using var response = await client.PostAsync(
            new Uri($"{Path}/domain-rule", UriKind.Relative), null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        // An expected outcome, not a failure of the app: no Error event (no exception handler noise in Seq)
        Assert.DoesNotContain(api.Logs.Events, logEvent =>
            logEvent.Level >= LogEventLevel.Error && logEvent.Exception is DomainException);
    }

    [Fact]
    public async Task Post_ResultFieldError_Returns422WithCamelCaseFieldError()
    {
        using var client = api.CreateApiClient();

        using var response = await client.PostAsJsonAsync(
            new Uri($"{Path}/assignments", UriKind.Relative),
            new { technicianId = Guid.CreateVersion7() },
            TestContext.Current.CancellationToken);

        var problem = await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity);
        var error = Assert.Single(problem.FieldErrors());
        Assert.Equal("technicianId", error.Key);
        Assert.Equal([AssignTechnician.Message], error.Value);
    }

    [Fact]
    public async Task Post_ConcurrencyConflict_Returns409()
    {
        using var client = api.CreateApiClient();

        using var response = await client.PostAsync(
            new Uri($"{Path}/stale", UriKind.Relative), null, TestContext.Current.CancellationToken);

        var problem = await response.ReadProblemAsync(HttpStatusCode.Conflict);
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("detail").GetString()));
    }

    [Fact]
    public async Task Post_InvalidBody_Returns400WithCamelCaseFieldErrors()
    {
        using var client = api.CreateApiClient();

        using var response = await client.PostAsJsonAsync(
            new Uri($"{Path}/samples", UriKind.Relative),
            new { name = "", at = "2026-01-14T09:00:00Z", on = "2026-01-14", status = "New", tags = _emptyTag },
            TestContext.Current.CancellationToken);

        var problem = await response.ReadProblemAsync(HttpStatusCode.BadRequest);
        var errors = problem.FieldErrors();
        Assert.Equal(["name", "tags[0]"], errors.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Get_UnbindableQueryValue_Returns400Problem()
    {
        using var client = api.CreateApiClient();

        using var response = await client.GetAsync(
            new Uri($"{Path}/samples?pageSize=abc", UriKind.Relative), TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_UnhandledException_Returns500WithoutExceptionDetails()
    {
        using var client = api.CreateApiClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        // The shared host runs as "Testing": what any environment other than Development returns
        using var response = await client.GetAsync(new Uri($"{Path}/crash", UriKind.Relative), cancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.InternalServerError);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.DoesNotContain(ConventionTestEndpoints.CrashDetail, body, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(InvalidOperationException), body, StringComparison.Ordinal);
    }
}
