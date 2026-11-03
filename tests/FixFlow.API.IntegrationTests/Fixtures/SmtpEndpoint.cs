using System.Net;
using System.Net.Sockets;

namespace FixFlow.API.IntegrationTests.Fixtures;

/// <summary>An SMTP server's address, for <see cref="FixFlowApiFactory.WithRealEmail"/>.</summary>
public sealed record SmtpEndpoint(string Host, int Port)
{
    /// <summary>
    /// A loopback port that was free a moment ago, so nothing listens on it: a server that is down. Another process
    /// could take it in between, but the OS hands out ephemeral ports in turn, so not soon.
    /// </summary>
    public static SmtpEndpoint Closed()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return new SmtpEndpoint(IPAddress.Loopback.ToString(), port);
    }
}
