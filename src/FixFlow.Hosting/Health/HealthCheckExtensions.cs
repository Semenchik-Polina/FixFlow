using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FixFlow.Hosting.Health;

public static class HealthCheckExtensions
{
    private const string ReadyTag = "ready";

    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Readiness checks for PostgreSQL and Blob Storage. The connection strings are resolved on the first probe, so a
    /// run that never probes (the build-time OpenAPI document) needs no settings.
    /// </summary>
    public static IServiceCollection AddFixFlowHealthChecks(
        this IServiceCollection services,
        Func<IServiceProvider, string> postgresConnectionString,
        Func<IServiceProvider, string> blobConnectionString)
    {
        // Its own client, created on first use: a probe shouldn't retry. T4.x can switch to the Infrastructure client.
        BlobServiceClient? blobClient = null;
        BlobServiceClient BlobClient(IServiceProvider provider) =>
            LazyInitializer.EnsureInitialized(ref blobClient, () =>
            {
                var options = new BlobClientOptions();
                options.Retry.MaxRetries = 0;
                return new BlobServiceClient(blobConnectionString(provider), options);
            });

        services.AddHealthChecks()
            // This overload builds its data source (and pool) once, on the first probe
            .AddNpgSql(postgresConnectionString, name: "postgres", tags: [ReadyTag], timeout: _timeout)
            .AddAzureBlobStorage(BlobClient, name: "blob", tags: [ReadyTag], timeout: _timeout);

        return services;
    }

    /// <summary>
    /// <c>/health/live</c> runs no checks: 200 while the process serves requests. <c>/health/ready</c> runs the
    /// readiness checks: 503 when one is unhealthy. Both are anonymous, and the default writer returns only the
    /// status, never exception details (S15).
    /// </summary>
    public static IEndpointRouteBuilder MapFixFlowHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
            .AllowAnonymous();

        endpoints
            .MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadyTag) })
            .AllowAnonymous();

        return endpoints;
    }
}
