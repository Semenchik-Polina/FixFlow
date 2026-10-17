using FixFlow.API.Auth;
using FixFlow.API.Configuration;
using FixFlow.API.Endpoints;
using FixFlow.API.Errors;
using FixFlow.API.Json;
using FixFlow.API.OpenApi;
using FixFlow.Application;
using FixFlow.Hosting.Health;
using FixFlow.Hosting.Logging;
using FixFlow.Hosting.Telemetry;
using NodaTime;
using Serilog;
using Serilog.Formatting.Compact;

const string ServiceName = "fixflow-api";

// Only for failures before the host's logger exists (that one is set up by AddFixFlowSerilog)
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new RenderedCompactJsonFormatter())
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddFixFlowSerilog(ServiceName);
    builder.AddFixFlowTelemetry(ServiceName);
    if (!BuildTimeDocument.IsGenerating)
    {
        builder.Services.AddFixFlowHealthChecks(
            builder.Configuration.GetPostgresConnectionString(),
            builder.Configuration.GetRequiredConnectionString("Blob"));
    }

    builder.Services.AddSingleton<IClock>(SystemClock.Instance);
    builder.Services.AddFixFlowApplication();
    builder.Services.AddFixFlowJson();
    builder.Services.AddFixFlowErrorHandling();
    builder.Services.AddFixFlowAuth();
    builder.Services.AddFixFlowOpenApi();
    // Endpoint modules: builder.Services.AddEndpointModule<...>() (from T1.3)

    var app = builder.Build();

    // Outermost, so it logs the final status code (a 422 at Information, a 500 at Error)
    app.UseFixFlowRequestLogging();
    app.UseFixFlowErrorHandling();
    app.UseAuthentication();
    // T1.5: the organization context middleware goes here
    // Needs the user's claims: keep it after authentication and the organization context middleware
    app.UseFixFlowRequestLogContext();
    app.UseAuthorization();

    if (!BuildTimeDocument.IsGenerating)
    {
        app.MapFixFlowHealthChecks();
    }

    app.MapFixFlowApi();

    app.Run();
}
// WebApplicationFactory stops the host with HostAbortedException on purpose
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
