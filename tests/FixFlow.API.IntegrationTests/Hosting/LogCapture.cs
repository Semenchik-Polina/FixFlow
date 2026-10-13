using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using FixFlow.Hosting.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace FixFlow.API.IntegrationTests.Hosting;

internal static class LogCapture
{
    /// <summary>
    /// Writes one event through the app's enrichers (masking included) and returns it as the JSON the app writes
    /// to the console.
    /// </summary>
    public static JsonElement Render(Action<ILogger> write)
    {
        var sink = new CollectingSink();
        using (var logger = new LoggerConfiguration().EnrichWithFixFlowDefaults("test").WriteTo.Sink(sink).CreateLogger())
        {
            write(logger);
        }

        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        new RenderedCompactJsonFormatter().Format(Assert.Single(sink.Events), writer);
        using var json = JsonDocument.Parse(writer.ToString());
        return json.RootElement.Clone();
    }
}

internal sealed class CollectingSink : ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> _events = new();

    public IReadOnlyCollection<LogEvent> Events => _events;

    public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);
}
