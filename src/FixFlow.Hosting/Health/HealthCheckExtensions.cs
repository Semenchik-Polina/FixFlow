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

    /// <summary>Readiness checks for PostgreSQL and Blob Storage.</summary>
    public static IServiceCollection AddFixFlowHealthChecks(
        this IServiceCollection services, string postgresConnectionString, string blobConnectionString)
    {
        // Its own client, created on first use: a probe shouldn't retry. T4.x can switch to the Infrastructure client.
        var blobClient = new Lazy<BlobServiceClient>(() =>
        {
            var options = new BlobClientOptions();
            options.Retry.MaxRetries = 0;
            return new BlobServiceClient(blobConnectionString, options);
        });

        services.AddHealthChecks()
            .AddNpgSql(postgresConnectionString, name: "postgres", tags: [ReadyTag], timeout: _timeout)
            .AddAzureBlobStorage(_ => blobClient.Value, name: "blob", tags: [ReadyTag], timeout: _timeout);

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
