using System.Diagnostics;

namespace FixFlow.API.IntegrationTests.Hosting;

public class TraceContextTests
{
    [Fact]
    public void Log_InsideActivity_WritesItsTraceAndSpanIds()
    {
        using var activity = new Activity("request").Start();

        var output = LogCapture.Render(log => log.Information("Handling the request"));

        Assert.Equal(activity.TraceId.ToString(), output.GetProperty("@tr").GetString());
        Assert.Equal(activity.SpanId.ToString(), output.GetProperty("@sp").GetString());
    }
}
