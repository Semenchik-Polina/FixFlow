using FixFlow.API.Endpoints;
using FixFlow.API.IntegrationTests.Fixtures;
using FixFlow.Application.Organizations.Context;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FixFlow.API.IntegrationTests.Organizations;

/// <summary>
/// Test-only endpoints (registered by <see cref="FixFlowApiFactory"/>) around the organization context: the membership
/// resolution as the organization context middleware runs it, for the signed-in caller.
/// </summary>
public sealed class OrganizationContextTestEndpoints : IEndpointModule
{
    public const string Path = $"{EndpointModuleExtensions.ApiRoot}/test-organization";

    public string Prefix => "/test-organization";

    public void Map(RouteGroupBuilder group)
    {
        // Always a 200: a 404 would look like an unmatched route, which a signed-in caller also gets
        group.MapGet(
            "/memberships/{slug}",
            async (string slug, ActiveMembershipResolver resolver, CancellationToken cancellationToken) =>
                TypedResults.Ok(new MembershipLookup(await resolver.FindAsync(slug, cancellationToken))));
    }
}

/// <param name="Membership">The caller's active membership, or null.</param>
public sealed record MembershipLookup(ActiveMembership? Membership);
