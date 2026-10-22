using System.Net;
using FixFlow.API.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Serilog.Events;

namespace FixFlow.API.IntegrationTests.Hosting;

/// <summary>
/// The SPA as the image serves it from wwwroot, here a stand-in web root: these tests cover the API's hosting rules,
/// not the React app. Without a web root (the shared host, and the API on the host) nothing is served:
/// <see cref="FixFlowApiFactoryTests"/> still gets 401 for an unknown route.
/// </summary>
public class SpaHostingTests(FixFlowApiFactory api)
{
    private const string IndexTitle = "fixflow-test-spa";

    private static readonly string _webRoot = Path.Combine(AppContext.BaseDirectory, "Hosting", "TestWebRoot");

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("/")]
    [InlineData("/x/requests")]
    [InlineData("/apiary/requests")]
    public async Task Get_ClientRouteAnonymously_ReturnsIndexHtmlWithoutCaching(string path)
    {
        await using var app = WithSpa();
        using var client = app.CreateApiClient();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), _ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains(IndexTitle, await response.Content.ReadAsStringAsync(_ct), StringComparison.Ordinal);
        Assert.True(response.Headers.CacheControl?.NoCache);
    }

    [Fact]
    public async Task Get_ClientRoute_LogsTheRequestedPath()
    {
        var path = $"/{TestData.OrganizationSlug()}/requests";
        await using var app = WithSpa();
        using var client = app.CreateApiClient();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), _ct);

        // Not "/index.html": serving the SPA doesn't rewrite the request
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(api.Logs.Events, logEvent =>
            logEvent.Properties.TryGetValue("RequestPath", out var value)
            && value is ScalarValue { Value: string requestPath }
            && requestPath == path);
    }

    [Theory]
    [InlineData("/api")]
    [InlineData("/api/v1/nope")]
    [InlineData("/API/v1/nope")]
    [InlineData("/health/nope")]
    [InlineData("/hubs/notifications")]
    public async Task Get_UnknownServerRouteAnonymously_Returns401(string path)
    {
        await using var app = WithSpa();
        using var client = app.CreateApiClient();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), _ct);

        // Not index.html: the fallback policy still answers (plan P10)
        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_ClientRoute_Returns404()
    {
        await using var app = WithSpa();
        using var client = app.CreateApiClient();

        using var response = await client.PostAsync(new Uri("/x/requests", UriKind.Relative), content: null, _ct);

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_HealthLive_ReturnsHealthStatus()
    {
        await using var app = WithSpa();
        using var client = app.CreateApiClient();

        using var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative), _ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(_ct));
    }

    [Fact]
    public async Task Get_IndexHtmlByName_ReturnsItWithoutCaching()
    {
        await using var app = WithSpa();
        using var client = app.CreateApiClient();

        using var response = await client.GetAsync(new Uri("/index.html", UriKind.Relative), _ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoCache);
    }

    [Fact]
    public async Task Get_AssetAnonymously_ReturnsFileCachedAsImmutable()
    {
        await using var app = WithSpa();
        using var client = app.CreateApiClient();

        using var response = await client.GetAsync(new Uri("/assets/app.js", UriKind.Relative), _ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/javascript", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(TimeSpan.FromDays(365), response.Headers.CacheControl?.MaxAge);
        Assert.Contains("immutable", response.Headers.CacheControl?.Extensions.Select(e => e.Name) ?? []);
    }

    [Fact]
    public async Task Get_MissingAssetAnonymously_Returns401()
    {
        await using var app = WithSpa();
        using var client = app.CreateApiClient();

        using var response = await client.GetAsync(new Uri("/assets/missing.js", UriKind.Relative), _ct);

        // A path that looks like a file never falls back to index.html: it is an unknown route (plan P10)
        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
    }

    private WebApplicationFactory<Program> WithSpa() =>
        api.WithWebHostBuilder(builder => builder.UseWebRoot(_webRoot));
}
