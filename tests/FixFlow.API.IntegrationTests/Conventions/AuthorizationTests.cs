using System.Net;
using FixFlow.API.Endpoints;
using FixFlow.API.IntegrationTests.Fixtures;

namespace FixFlow.API.IntegrationTests.Conventions;

public class AuthorizationTests(FixFlowApiFactory api)
{
    [Fact]
    public async Task Get_EndpointNotAllowListedAnonymously_Returns401WithoutRedirect()
    {
        using var client = api.CreateApiClient();

        using var response = await client.GetAsync(
            new Uri($"{ConventionTestEndpoints.Path}/protected", UriKind.Relative), TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Get_UnknownRouteAnonymously_Returns401()
    {
        using var client = api.CreateApiClient();

        // The fallback policy covers unmatched routes too, so anonymous callers can't probe which ones exist
        using var response = await client.GetAsync(
            new Uri($"{EndpointModuleExtensions.ApiRoot}/{TestData.Name("nope").Replace(' ', '-')}", UriKind.Relative),
            TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
    }
}
