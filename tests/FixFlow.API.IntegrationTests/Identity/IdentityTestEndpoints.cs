using FixFlow.API.Endpoints;
using FixFlow.API.IntegrationTests.Fixtures;
using FixFlow.Application.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FixFlow.API.IntegrationTests.Identity;

/// <summary>
/// A test-only endpoint (registered by <see cref="FixFlowApiFactory"/>) that reads the session back: the current user
/// and every claim of the principal, to check that none is personal data (S26).
/// </summary>
public sealed class IdentityTestEndpoints : IEndpointModule
{
    public const string Path = $"{EndpointModuleExtensions.ApiRoot}/test-identity";

    public string Prefix => "/test-identity";

    public void Map(RouteGroupBuilder group)
    {
        group.MapGet("/session", (ICurrentUser currentUser, HttpContext context) => TypedResults.Ok(
            new Session(currentUser.UserId!.Value, [.. context.User.Claims.Select(claim => claim.Value)])));
    }
}

/// <param name="ClaimValues">Every claim of the request's principal, to check that none is personal data.</param>
public sealed record Session(Guid UserId, string[] ClaimValues);
