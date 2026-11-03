using System.Collections.Concurrent;
using System.Globalization;
using Serilog.Core;
using Serilog.Events;

namespace FixFlow.API.IntegrationTests.Fixtures;

/// <summary>Collects log events in memory. Safe to share between parallel tests: it only appends.</summary>
public sealed class TestLogSink : ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> _events = new();

    public IReadOnlyCollection<LogEvent> Events => _events;

    public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);

    /// <summary>
    /// Events whose message, properties or exception contain <paramref name="value"/>, in any case: Identity stores
    /// emails uppercased.
    /// </summary>
    public IEnumerable<LogEvent> Mentioning(string value) =>
        _events.Where(logEvent => Text(logEvent).Contains(value, StringComparison.OrdinalIgnoreCase));

    private static string Text(LogEvent logEvent) => string.Join(
        '\n',
        [
            logEvent.RenderMessage(CultureInfo.InvariantCulture),
            .. logEvent.Properties.Values.Select(value => value.ToString()),
            logEvent.Exception?.ToString() ?? "",
        ]);
}
