using System.Net;
using FixFlow.API.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FixFlow.API.IntegrationTests.Hosting;

public class HealthEndpointTests(FixFlowApiFactory api)
{
    [Fact]
    public async Task HealthReady_InfrastructureUp_Returns200()
    {
        using var client = api.CreateApiClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        using var ready = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), cancellationToken);

        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal("Healthy", await ready.Content.ReadAsStringAsync(cancellationToken));
    }

    [Fact]
    public async Task HealthEndpoints_PostgresUnreachable_LiveIs200AndReadyIs503()
    {
        // Its own host, not the shared one: Postgres must be down. "Testing", not the factory's default
        // "Development": no Seq or OTLP settings, no user-secrets. Nothing listens on port 1.
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Testing")
            .UseSetting("ConnectionStrings:FixFlow", "Host=127.0.0.1;Port=1;Database=fixflow;Username=fixflow_app;Timeout=2")
            .UseSetting("Database:Password", "unused")
            .UseSetting("ConnectionStrings:Blob", "UseDevelopmentStorage=true"));
        using var client = factory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        using var live = await client.GetAsync(new Uri("/health/live", UriKind.Relative), cancellationToken);
        using var ready = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), cancellationToken);

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        // Status only, no exception details (S15)
        Assert.Equal("Unhealthy", await ready.Content.ReadAsStringAsync(cancellationToken));
    }
}
