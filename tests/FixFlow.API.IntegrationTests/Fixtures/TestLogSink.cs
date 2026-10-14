using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

namespace FixFlow.API.IntegrationTests.Fixtures;

/// <summary>Collects log events in memory. Safe to share between parallel tests: it only appends.</summary>
public sealed class TestLogSink : ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> _events = new();

    public IReadOnlyCollection<LogEvent> Events => _events;

    public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);
}
