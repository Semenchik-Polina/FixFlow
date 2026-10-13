using FixFlow.API.Configuration;
using FixFlow.Hosting.Health;
using FixFlow.Hosting.Logging;
using FixFlow.Hosting.Telemetry;
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
    builder.Services.AddFixFlowHealthChecks(
        builder.Configuration.GetPostgresConnectionString(),
        builder.Configuration.GetRequiredConnectionString("Blob"));

    var app = builder.Build();

    app.UseFixFlowRequestLogging();
    // Needs the user's claims: keep it after authentication and the organization context middleware (T1.x)
    app.UseFixFlowRequestLogContext();

    app.MapFixFlowHealthChecks();
    app.MapGet("/", () => "Hello World!");

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
