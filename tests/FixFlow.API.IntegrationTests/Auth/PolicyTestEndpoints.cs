using FixFlow.API.Auth;
using FixFlow.API.Endpoints;
using FixFlow.API.Organizations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FixFlow.API.IntegrationTests.Auth;

/// <summary>
/// Test-only endpoints (registered by <see cref="Fixtures.FixFlowApiFactory"/>), one per role policy:
/// <c>GET /test-policies/{policy}</c> answers 204 to a caller the policy lets through. They need an organization, as
/// every <c>/api/v1</c> endpoint does by default. One more runs a role policy without an organization context.
/// </summary>
public sealed class PolicyTestEndpoints : IEndpointModule
{
    public const string Path = $"{EndpointModuleExtensions.ApiRoot}/test-policies";

    /// <summary>Organization-less, behind <see cref="Policies.CanManageOrganization"/>: no caller has roles there.</summary>
    public const string WithoutOrganizationPath = $"{Path}/without-organization";

    public string Prefix => "/test-policies";

    /// <summary>The endpoint behind <paramref name="policy"/>.</summary>
    public static Uri PathOf(string policy) => new($"{Path}/{policy}", UriKind.Relative);

    public void Map(RouteGroupBuilder group)
    {
        foreach (var policy in Policies.Roles.Keys)
        {
            group.MapGet($"/{policy}", () => TypedResults.NoContent()).RequireAuthorization(policy);
        }

        group.MapGet("/without-organization", () => TypedResults.NoContent())
            .WithoutOrganization()
            .RequireAuthorization(Policies.CanManageOrganization);
    }
}
