using FixFlow.API.Endpoints;
using FixFlow.API.IntegrationTests.Fixtures;
using FixFlow.API.Organizations;
using FixFlow.Application.Auth;
using FixFlow.Application.Organizations.Context;
using FixFlow.Application.Persistence;
using FixFlow.Domain.Organizations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FixFlow.API.IntegrationTests.Organizations;

/// <summary>
/// Test-only endpoints (registered by <see cref="FixFlowApiFactory"/>) around the organization context. They need one,
/// as every <c>/api/v1</c> endpoint does by default, and read back what the middleware gave the request. The membership
/// lookup is the exception: it runs the resolution by hand, for any slug, so it is organization-less.
/// </summary>
public sealed partial class OrganizationContextTestEndpoints : IEndpointModule
{
    public const string Path = $"{EndpointModuleExtensions.ApiRoot}/test-organization";

    public string Prefix => "/test-organization";

    public void Map(RouteGroupBuilder group)
    {
        // Always a 200: a 404 would look like an unmatched route, which a signed-in caller also gets
        group.MapGet(
                "/memberships/{slug}",
                async (string slug, ActiveMembershipResolver resolver, CancellationToken cancellationToken) =>
                    TypedResults.Ok(new MembershipLookup(await resolver.FindAsync(slug, cancellationToken))))
            .WithoutOrganization();

        // Logs one event, to check that it carries the organization
        group.MapGet("/context", (ICurrentUser currentUser, ILogger<OrganizationContextTestEndpoints> logger) =>
        {
            LogContextRead(logger);
            return TypedResults.Ok(new OrganizationContextRead(currentUser.OrganizationId, [.. currentUser.Roles.Order()]));
        });

        // Through the organization filter, which reads the context
        group.MapGet("/memberships", async (IFixFlowDbContext db, CancellationToken cancellationToken) =>
            TypedResults.Ok(await db.OrganizationMemberships
                .AsNoTracking()
                .Select(membership => membership.UserId)
                .ToArrayAsync(cancellationToken)));

        // Must throw (500): a scope never replaces the organization a request resolved (plan P25)
        group.MapPost("/trusted-scope", (ITrustedOrganizationScope trustedScope) =>
        {
            using var scope = trustedScope.Open(Guid.CreateVersion7());
            return TypedResults.NoContent();
        });
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Test endpoint read the organization context")]
    private static partial void LogContextRead(ILogger logger);
}

/// <param name="Membership">The caller's active membership, or null.</param>
public sealed record MembershipLookup(ActiveMembership? Membership);

/// <summary>The request's organization context as <see cref="ICurrentUser"/> gives it.</summary>
/// <param name="Roles">In the order of <see cref="Role"/>.</param>
public sealed record OrganizationContextRead(Guid? OrganizationId, Role[] Roles);
