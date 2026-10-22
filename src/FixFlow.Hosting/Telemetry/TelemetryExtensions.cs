using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace FixFlow.Hosting.Telemetry;

public static class TelemetryExtensions
{
    /// <summary>
    /// OpenTelemetry traces and metrics, exported over OTLP (Seq locally) only when <c>Otlp:Endpoint</c> is set,
    /// so tests and CI don't try to reach a collector. Seq's API key is sent when <c>Seq:ApiKey</c> is set.
    /// The instrumentations' default URL-query redaction stays on (S26: SAS URLs carry a signature).
    /// </summary>
    public static IHostApplicationBuilder AddFixFlowTelemetry(this IHostApplicationBuilder builder, string serviceName)
    {
        var endpoint = builder.Configuration["Otlp:Endpoint"];
        var apiKey = builder.Configuration["Seq:ApiKey"];
        var export = !string.IsNullOrWhiteSpace(endpoint);

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName, serviceVersion: Assembly.GetEntryAssembly()?.GetName().Version?.ToString())
                .AddAttributes([new("deployment.environment.name", builder.Environment.EnvironmentName)]))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(options =>
                        options.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
                    // Both emit spans natively (HttpClient since .NET 9)
                    .AddSource("System.Net.Http", "Npgsql");

                if (export)
                {
                    tracing.AddOtlpExporter(options => ConfigureOtlp(options, endpoint!, "traces", apiKey));
                }
            })
            .WithMetrics(metrics =>
            {
                metrics.AddMeter(
                    "Microsoft.AspNetCore.Hosting",
                    "Microsoft.AspNetCore.Server.Kestrel",
                    "System.Net.Http",
                    "System.Runtime",
                    "Npgsql");

                if (export)
                {
                    metrics.AddOtlpExporter((options, reader) =>
                    {
                        ConfigureOtlp(options, endpoint!, "metrics", apiKey);
                        // Seq charts delta sums best; cumulative ones arrive as sawtooth gauges
                        reader.TemporalityPreference = MetricReaderTemporalityPreference.Delta;
                        reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 15_000;
                    });
                }
            });

        return builder;
    }

    // An endpoint set in code gets no signal path appended, so add /v1/{signal} here
    private static void ConfigureOtlp(OtlpExporterOptions options, string endpoint, string signal, string? apiKey)
    {
        options.Endpoint = new Uri($"{endpoint.TrimEnd('/')}/v1/{signal}");
        options.Protocol = OtlpExportProtocol.HttpProtobuf;

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            options.Headers = $"X-Seq-ApiKey={apiKey}";
        }
    }
}
