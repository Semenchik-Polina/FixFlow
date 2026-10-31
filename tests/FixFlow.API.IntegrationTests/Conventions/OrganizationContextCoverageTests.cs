using FixFlow.API.Endpoints;
using FixFlow.API.IntegrationTests.Fixtures;
using FixFlow.API.Organizations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FixFlow.API.IntegrationTests.Conventions;

/// <summary>
/// Which API endpoints run without an organization context, read from the host's endpoints, as the middleware reads
/// their metadata. Needing one is the default, so an endpoint is organization-less only by choice
/// (<see cref="OrganizationContextExtensions.WithoutOrganization{TBuilder}"/>), and the choice is made here too.
/// The test modules (<c>/api/v1/test-*</c>) aren't part of the API. T1.6 adds the endpoints' policies.
/// </summary>
public class OrganizationContextCoverageTests(FixFlowApiFactory api)
{
    private const string TestModules = $"{EndpointModuleExtensions.ApiRoot}/test-";

    [Fact]
    public void OrganizationLessEndpoints_AreTheAllowList()
    {
        string[] allowed =
        [
            // Before an organization (signup, login, the password reset) or about the session alone (logout)
            "POST /api/v1/auth/signup",
            "POST /api/v1/auth/login",
            "POST /api/v1/auth/logout",
            "POST /api/v1/auth/forgot-password",
            "POST /api/v1/auth/reset-password",
            // The caller's own account, across all their organizations
            "GET /api/v1/me",
            "POST /api/v1/me/password",
            "PUT /api/v1/me/name",
            // A new organization: the handler opens a trusted scope for it (plan P25). T3.2 adds the invite endpoints.
            "POST /api/v1/organizations",
        ];

        var organizationLess = ApiEndpoints().Where(endpoint => !RequiresOrganization(endpoint)).SelectMany(Routes);

        Assert.Equal(allowed.Order(StringComparer.Ordinal), organizationLess.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void AnonymousEndpoints_AreOrganizationLess()
    {
        var anonymous = ApiEndpoints().Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null).ToList();

        // An anonymous caller has no memberships, so it could never get a context: the middleware would let it through
        // with none
        Assert.NotEmpty(anonymous);
        Assert.Empty(anonymous.Where(RequiresOrganization).SelectMany(Routes));
    }

    /// <summary>The endpoints under <c>/api/v1</c>, without the test modules.</summary>
    private IEnumerable<RouteEndpoint> ApiEndpoints() => api.Services
        .GetRequiredService<EndpointDataSource>()
        .Endpoints
        .OfType<RouteEndpoint>()
        .Where(endpoint => endpoint.RoutePattern.RawText is { } path
            && path.StartsWith($"{EndpointModuleExtensions.ApiRoot}/", StringComparison.Ordinal)
            && !path.StartsWith(TestModules, StringComparison.Ordinal));

    // As the middleware decides: the last metadata wins, and an endpoint without any needs no context
    private static bool RequiresOrganization(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetMetadata<OrganizationContextMetadata>() is { IsRequired: true };

    /// <summary>
    /// <c>"GET /api/v1/me"</c>, one per method. A module's <c>""</c> endpoint has a trailing slash in its raw text, which
    /// its route doesn't need.
    /// </summary>
    private static IEnumerable<string> Routes(RouteEndpoint endpoint) =>
        (endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? ["*"])
        .Select(method => $"{method} {endpoint.RoutePattern.RawText?.TrimEnd('/')}");
}
