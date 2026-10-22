using Microsoft.AspNetCore.Mvc.Testing;

namespace FixFlow.API.IntegrationTests.Fixtures;

public static class WebApplicationFactoryExtensions
{
    /// <summary>
    /// A client for one user session: its own cookies, no redirects followed. Works on the shared host and on hosts
    /// derived from it with <c>WithWebHostBuilder</c>.
    /// </summary>
    public static HttpClient CreateApiClient(this WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            // The auth cookie is Secure (S1), and CookieContainer only sends Secure cookies back over https
            BaseAddress = new Uri("https://localhost"),
            // The API answers 401/403 itself; following a redirect would hide that
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
}
