using System.Security.Claims;
using FixFlow.API.Auth;
using FixFlow.Application.Auth;
using FixFlow.Application.Organizations.Context;
using Microsoft.AspNetCore.Mvc;

namespace FixFlow.API.Organizations;

/// <summary>
/// Gives a request the organization context of §8 when its endpoint needs one (<see cref="OrganizationContextMetadata"/>):
/// the organization that <see cref="OrganizationContext.HeaderName"/> names by its slug, if the caller is an active
/// member of it. Its id and the caller's roles there go on an identity of their own, added to the request's user, where
/// <see cref="ICurrentUser"/> reads them.
/// </summary>
/// <remarks>
/// <para>
/// A middleware, not an <c>IClaimsTransformation</c>: authentication runs the transformation before it sets the user,
/// and the membership lookup reads the user (plan T1.5).
/// </para>
/// <para>
/// Anonymous callers pass through, so authorization answers them 401 and they can't probe for slugs. A missing, empty or
/// repeated header is a client bug: 400. Every other miss (an unknown or invalid slug, another organization's, a
/// suspended membership) is one bare 404, which the status code pages answer like any other, so it never tells which.
/// </para>
/// <para>
/// It never takes the trusted scope: an organization the caller names is never one the server derived (plan P25).
/// </para>
/// </remarks>
internal sealed partial class OrganizationContextMiddleware(
    RequestDelegate next,
    IProblemDetailsService problemDetailsService,
    ILogger<OrganizationContextMiddleware> logger)
{
    private const string AuthenticationType = "FixFlow.Organization";

    private const string MissingHeaderDetail =
        $"This endpoint needs the {OrganizationContext.HeaderName} header, with the slug of an organization.";

    private const string RepeatedHeaderDetail =
        $"Send the {OrganizationContext.HeaderName} header once, with the slug of one organization.";

    public async Task InvokeAsync(HttpContext context, ActiveMembershipResolver resolver, ICurrentUser currentUser)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<OrganizationContextMetadata>() is not { IsRequired: true }
            || context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var header = context.Request.Headers[OrganizationContext.HeaderName];
        if (header is not [{ } slug] || string.IsNullOrWhiteSpace(slug))
        {
            LogInvalidHeader(currentUser.UserId);
            await WriteBadRequestAsync(context, header.Count > 1 ? RepeatedHeaderDetail : MissingHeaderDetail);
            return;
        }

        var membership = await resolver.FindAsync(slug, context.RequestAborted);
        if (membership is null)
        {
            LogNoMembership(currentUser.UserId, slug);
            // Bare, so the status code pages write the Problem Details of any other 404
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.User.AddIdentity(CreateIdentity(membership));
        await next(context);
    }

    // Its own identity, with FixFlow's role claim type: IsInRole and RequireRole see this organization's roles only
    private static ClaimsIdentity CreateIdentity(ActiveMembership membership) => new(
        [
            new Claim(FixFlowClaimTypes.OrganizationId, membership.OrganizationId.ToString()),
            .. membership.Roles.Select(role => new Claim(FixFlowClaimTypes.Role, role.ToString())),
        ],
        AuthenticationType,
        nameType: null,
        roleType: FixFlowClaimTypes.Role);

    private async Task WriteBadRequestAsync(HttpContext context, string detail)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        // False only when the client accepts no JSON: the status code alone still answers it
        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails { Status = StatusCodes.Status400BadRequest, Detail = detail },
        });
    }

    // Ids only (S26). The request completion event of a refused request has no UserId: the log context comes later.
    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "No organization context for user {UserId}: the organization header is missing, empty or repeated")]
    private partial void LogInvalidHeader(Guid? userId);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "No organization context for user {UserId}: no active membership in {OrganizationSlug}")]
    private partial void LogNoMembership(Guid? userId, string organizationSlug);
}
