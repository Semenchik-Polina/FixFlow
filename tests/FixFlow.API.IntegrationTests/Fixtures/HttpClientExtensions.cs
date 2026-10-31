using FixFlow.API.Organizations;

namespace FixFlow.API.IntegrationTests.Fixtures;

public static class HttpClientExtensions
{
    /// <summary>The organization context header (CLAUDE.md §3).</summary>
    public const string OrganizationHeader = OrganizationContext.HeaderName;

    /// <summary>Sends <c>X-Organization: {slug}</c> on every request from this client.</summary>
    public static HttpClient WithOrganization(this HttpClient client, string slug)
    {
        client.DefaultRequestHeaders.Remove(OrganizationHeader);
        client.DefaultRequestHeaders.Add(OrganizationHeader, slug);
        return client;
    }

    /// <summary>Sends <c>X-Organization: {slug}</c> on this request only, e.g. a mismatched org.</summary>
    public static HttpRequestMessage WithOrganization(this HttpRequestMessage request, string slug)
    {
        request.Headers.Remove(OrganizationHeader);
        request.Headers.Add(OrganizationHeader, slug);
        return request;
    }
}
