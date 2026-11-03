using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FixFlow.API.IntegrationTests.Fixtures;

public static class WebApplicationFactoryExtensions
{
    /// <summary>
    /// The test-only header that <see cref="FixFlowApiFactory"/> turns into the connection's client address, which
    /// TestServer leaves null. The app never reads it.
    /// </summary>
    public const string ClientAddressHeader = "X-Test-Client-Ip";

    private static int _lastClient;

    /// <summary>
    /// A client for one user session: its own cookies, no redirects followed, and an address in a /64 of its own
    /// (<see cref="NextClientAddress"/>), so parallel tests never share a rate limit budget. Works on the shared host
    /// and on hosts derived from it with <c>WithWebHostBuilder</c>.
    /// </summary>
    public static HttpClient CreateApiClient(this WebApplicationFactory<Program> factory) =>
        factory.CreateApiClient(NextClientAddress());

    /// <summary>A client for one user session from <paramref name="clientAddress"/>.</summary>
    public static HttpClient CreateApiClient(this WebApplicationFactory<Program> factory, IPAddress clientAddress)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            // The auth cookie is Secure (S1), and CookieContainer only sends Secure cookies back over https
            BaseAddress = new Uri("https://localhost"),
            // The API answers 401/403 itself; following a redirect would hide that
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
        client.DefaultRequestHeaders.Add(ClientAddressHeader, clientAddress.ToString());
        return client;
    }

    /// <summary>
    /// <c>2001:db8:{n}::1</c> for the run's next n: a /64 of its own in the documentation prefix 2001:db8::/32 (D23).
    /// The rate limiter gives each /64 one budget, so two clients in one /64 would share it.
    /// </summary>
    public static IPAddress NextClientAddress()
    {
        var client = (uint)Interlocked.Increment(ref _lastClient);
        return IPAddress.Parse($"2001:db8:{client >> 16:x}:{client & 0xFFFF:x}::1");
    }
}
