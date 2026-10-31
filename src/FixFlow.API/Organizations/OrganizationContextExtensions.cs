using FixFlow.API.Endpoints;

namespace FixFlow.API.Organizations;

public static class OrganizationContextExtensions
{
    /// <summary>
    /// The endpoints need an organization context: a signed-in caller must name, in
    /// <see cref="OrganizationContext.HeaderName"/>, an organization they are an active member of. The default of every
    /// endpoint under <c>/api/v1</c> (<see cref="EndpointModuleExtensions.MapFixFlowApi"/>).
    /// </summary>
    public static TBuilder RequireOrganization<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(OrganizationContextMetadata.Required);

    /// <summary>
    /// The endpoints are organization-less (<c>/me</c>, auth, <c>POST /organizations</c>): the header is ignored, and
    /// the request gets no organization context. They must not touch organization-owned data, except through a trusted
    /// scope (plan P25). Overrides a group's <see cref="RequireOrganization{TBuilder}"/>.
    /// </summary>
    public static TBuilder WithoutOrganization<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(OrganizationContextMetadata.NotRequired);

    /// <summary>
    /// Gives the requests whose endpoint needs it an organization context (<see cref="OrganizationContextMiddleware"/>).
    /// Register it after authentication, which gives the caller, and before the request log context and authorization,
    /// which read the context's claims.
    /// </summary>
    public static IApplicationBuilder UseFixFlowOrganizationContext(this IApplicationBuilder app) =>
        app.UseMiddleware<OrganizationContextMiddleware>();
}
