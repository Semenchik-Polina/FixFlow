using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Enrichers.Sensitive;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace FixFlow.Hosting.Logging;

public static class LoggingExtensions
{
    /// <summary>
    /// Serilog for the host. Levels come from the <c>Serilog</c> configuration section; sinks and enrichers are set
    /// here, so configuration can't remove the masking. The static <see cref="Log.Logger"/> stays the bootstrap
    /// logger: the app logs through DI, which keeps several test hosts in one process independent.
    /// </summary>
    public static IHostApplicationBuilder AddFixFlowSerilog(this IHostApplicationBuilder builder, string applicationName)
    {
        var configuration = builder.Configuration;

        builder.Services.AddSerilog(
            (_, logger) => logger
                .ReadFrom.Configuration(configuration)
                .WriteToFixFlowSinks(configuration)
                .EnrichWithFixFlowDefaults(applicationName),
            preserveStaticLogger: true);

        return builder;
    }

    /// <summary>
    /// Enrichers shared by the app and the tests. Masking goes last, so it also sees the properties added by the
    /// enrichers before it (LogContext, configuration).
    /// </summary>
    public static LoggerConfiguration EnrichWithFixFlowDefaults(this LoggerConfiguration logger, string applicationName) =>
        logger
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", applicationName)
            .Enrich.WithSensitiveDataMasking(SensitiveDataMasking.Configure);

    /// <summary>
    /// One event per request: method, path without the query string, status code and elapsed time.
    /// Health probes are logged at Verbose so they don't flood Seq.
    /// </summary>
    public static IApplicationBuilder UseFixFlowRequestLogging(this IApplicationBuilder app) =>
        app.UseSerilogRequestLogging(options =>
        {
            // Without it the middleware writes to the static (bootstrap) logger: no Seq, no masking
            options.Logger = app.ApplicationServices.GetRequiredService<ILogger>();
            options.GetLevel = (context, _, exception) =>
                context.Request.Path.StartsWithSegments("/health") ? LogEventLevel.Verbose
                : exception is not null || context.Response.StatusCode >= 500 ? LogEventLevel.Error
                : LogEventLevel.Information;
        });

    /// <summary>
    /// Adds the caller's UserId and OrganizationId to the request's logs and trace. Register it after authentication
    /// and the organization context middleware, which put those claims on the user.
    /// </summary>
    public static IApplicationBuilder UseFixFlowRequestLogContext(this IApplicationBuilder app) =>
        app.UseMiddleware<RequestLogContextMiddleware>();

    private static LoggerConfiguration WriteToFixFlowSinks(this LoggerConfiguration logger, IConfiguration configuration)
    {
        logger.WriteTo.Console(new RenderedCompactJsonFormatter());

        var seqUrl = configuration["Seq:ServerUrl"];
        if (!string.IsNullOrWhiteSpace(seqUrl))
        {
            var apiKey = configuration["Seq:ApiKey"];
            logger.WriteTo.Seq(
                seqUrl,
                apiKey: string.IsNullOrWhiteSpace(apiKey) ? null : apiKey,
                formatProvider: CultureInfo.InvariantCulture);
        }

        return logger;
    }
}
