using FixFlow.API.Auth;
using FixFlow.API.Endpoints;
using FixFlow.API.IntegrationTests.Fixtures;
using FixFlow.API.Organizations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;

namespace FixFlow.API.IntegrationTests.Conventions;

/// <summary>
/// How the host's endpoints are authorized (S7, §8) and which run without an organization context, read from their
/// metadata as the middlewares read it. Every endpoint declares its authorization: an allow-listed
/// <c>AllowAnonymous</c>, or <c>RequireAuthorization</c>; the fallback policy only catches what slips through. An endpoint
/// that needs an organization names a role policy from <see cref="Policies"/>, and an organization-less one never does.
/// Organization-less endpoints are a choice (<see cref="OrganizationContextExtensions.WithoutOrganization{TBuilder}"/>)
/// made here too. The test modules (<c>/api/v1/test-*</c>) aren't part of the API.
/// </summary>
/// <remarks>
/// Each check is a function over endpoints that returns the routes breaking it, so the negative controls at the end
/// run the same checks on endpoints made up for them.
/// </remarks>
public class EndpointCoverageTests(FixFlowApiFactory api)
{
    private const string TestModules = $"{EndpointModuleExtensions.ApiRoot}/test-";

    /// <summary>S7's anonymous allow-list.</summary>
    private static readonly string[] _anonymous =
    [
        // Before an account or a session: all rate-limited (S6)
        "POST /api/v1/auth/signup",
        "POST /api/v1/auth/login",
        "POST /api/v1/auth/forgot-password",
        "POST /api/v1/auth/reset-password",
        // The container's probes
        "* /health/live",
        "* /health/ready",
        // The SPA's pages, mapped only where the SPA is served (not on this host)
        "* {*path:nonfile:clientRoute}",
    ];

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
        var anonymous = ApiEndpoints().Where(IsAnonymous).ToList();

        // An anonymous caller has no memberships, so it could never get a context: the middleware would let it through
        // with none
        Assert.NotEmpty(anonymous);
        Assert.Empty(anonymous.Where(RequiresOrganization).SelectMany(Routes));
    }

    [Fact]
    public void AnonymousEndpoints_AreAllowListed()
    {
        var endpoints = HostEndpoints().ToList();

        // The check reads real metadata: the API's anonymous endpoints are among them
        Assert.Contains("POST /api/v1/auth/login", endpoints.Where(IsAnonymous).SelectMany(Routes));
        Assert.Empty(AnonymousNotAllowListed(endpoints));
    }

    [Fact]
    public void Endpoints_DeclareTheirAuthorization() => Assert.Empty(WithoutExplicitAuthorization(HostEndpoints()));

    [Fact]
    public void OrganizationEndpoints_NameARolePolicy() => Assert.Empty(OrganizationEndpointsWithoutRolePolicy(HostEndpoints()));

    [Fact]
    public void OrganizationLessEndpoints_NameNoRolePolicy() =>
        Assert.Empty(OrganizationLessEndpointsWithRolePolicy(HostEndpoints()));

    [Fact]
    public void Endpoints_SetNoSchemesOrRoles() => Assert.Empty(WithSchemesOrRoles(HostEndpoints()));

    [Fact]
    public void Endpoints_UseNoInlinePolicies() => Assert.Empty(WithInlinePolicies(HostEndpoints()));

    [Fact]
    public void Endpoints_NameOnlyCatalogPolicies() => Assert.Empty(WithUnknownPolicies(HostEndpoints()));

    [Fact]
    public void Endpoints_NameOneRolePolicyAtMost() => Assert.Empty(WithSeveralRolePolicies(HostEndpoints()));

    [Fact]
    public async Task Policies_SetNoSchemes()
    {
        var provider = api.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var policies = new List<AuthorizationPolicy?>
        {
            await provider.GetDefaultPolicyAsync(),
            await provider.GetFallbackPolicyAsync(),
        };
        foreach (var name in Policies.Roles.Keys)
        {
            policies.Add(await provider.GetPolicyAsync(name));
        }

        // The policy evaluator would authenticate again, and rebuild the user without the organization's identity
        Assert.All(policies, policy => Assert.Empty(Assert.IsType<AuthorizationPolicy>(policy).AuthenticationSchemes));
    }

    // Negative controls: each check reports an endpoint made up to break it, and passes a well-formed one

    [Fact]
    public void WithoutExplicitAuthorization_FallbackPolicyOnly_IsReported()
    {
        var endpoint = Endpoint("/api/v1/widgets", OrganizationContextMetadata.Required);

        Assert.Equal(["GET /api/v1/widgets"], WithoutExplicitAuthorization([endpoint]));
    }

    [Fact]
    public void WithoutExplicitAuthorization_AnonymousAndAuthorized_IsReported()
    {
        var endpoint = Endpoint(
            "/api/v1/auth/login", OrganizationContextMetadata.NotRequired, new AllowAnonymousAttribute(), new AuthorizeAttribute());

        Assert.Equal(["GET /api/v1/auth/login"], WithoutExplicitAuthorization([endpoint]));
    }

    [Fact]
    public void AnonymousNotAllowListed_UnlistedAllowAnonymous_IsReported()
    {
        var endpoint = Endpoint("/api/v1/widgets", OrganizationContextMetadata.NotRequired, new AllowAnonymousAttribute());

        Assert.Equal(["GET /api/v1/widgets"], AnonymousNotAllowListed([endpoint]));
    }

    [Fact]
    public void OrganizationEndpointsWithoutRolePolicy_DefaultPolicyOnly_IsReported()
    {
        var endpoint = Endpoint("/api/v1/widgets", OrganizationContextMetadata.Required, new AuthorizeAttribute());

        Assert.Equal(["GET /api/v1/widgets"], OrganizationEndpointsWithoutRolePolicy([endpoint]));
    }

    [Fact]
    public void OrganizationLessEndpointsWithRolePolicy_RolePolicy_IsReported()
    {
        var endpoint = Endpoint(
            "/api/v1/me/widgets", OrganizationContextMetadata.NotRequired, new AuthorizeAttribute(Policies.CanManageProperties));

        Assert.Equal(["GET /api/v1/me/widgets"], OrganizationLessEndpointsWithRolePolicy([endpoint]));
    }

    [Theory]
    [InlineData("Identity.Application", null)]
    [InlineData(null, "Admin")]
    public void WithSchemesOrRoles_SchemesOrRoles_AreReported(string? schemes, string? roles)
    {
        var endpoint = Endpoint(
            "/api/v1/widgets",
            OrganizationContextMetadata.Required,
            new AuthorizeAttribute(Policies.CanManageProperties) { AuthenticationSchemes = schemes, Roles = roles });

        Assert.Equal(["GET /api/v1/widgets"], WithSchemesOrRoles([endpoint]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WithInlinePolicies_PolicyOrRequirementData_AreReported(bool requirementData)
    {
        // What RequireAuthorization(p => p.RequireRole(...)) and an attribute with requirements add, next to a role policy
        object inline = requirementData
            ? new ManagerRequirementData()
            : new AuthorizationPolicyBuilder().RequireRole("Manager").Build();
        var endpoint = Endpoint(
            "/api/v1/widgets", OrganizationContextMetadata.Required, new AuthorizeAttribute(Policies.CanManageProperties), inline);

        Assert.Equal(["GET /api/v1/widgets"], WithInlinePolicies([endpoint]));
    }

    [Fact]
    public void WithUnknownPolicies_PolicyOutsideTheCatalog_IsReported()
    {
        var endpoint = Endpoint(
            "/api/v1/widgets",
            OrganizationContextMetadata.Required,
            new AuthorizeAttribute(Policies.CanManageProperties),
            new AuthorizeAttribute("CanManageWidgets"));

        Assert.Equal(["GET /api/v1/widgets"], WithUnknownPolicies([endpoint]));
    }

    [Fact]
    public void WithSeveralRolePolicies_TwoRolePolicies_IsReported()
    {
        // A group's policy and its endpoint's: both must pass, and no role passes these two
        var endpoint = Endpoint(
            "/api/v1/widgets",
            OrganizationContextMetadata.Required,
            new AuthorizeAttribute(Policies.CanManageProperties),
            new AuthorizeAttribute(Policies.CanSubmitRequests));

        Assert.Equal(["GET /api/v1/widgets"], WithSeveralRolePolicies([endpoint]));
    }

    [Fact]
    public void Checks_OrganizationEndpointWithRolePolicy_PassesThemAll()
    {
        RouteEndpoint[] endpoint =
        [
            Endpoint("/api/v1/widgets", OrganizationContextMetadata.Required, new AuthorizeAttribute(Policies.CanManageProperties)),
        ];

        Assert.Empty(WithoutExplicitAuthorization(endpoint)
            .Concat(AnonymousNotAllowListed(endpoint))
            .Concat(OrganizationEndpointsWithoutRolePolicy(endpoint))
            .Concat(OrganizationLessEndpointsWithRolePolicy(endpoint))
            .Concat(WithSchemesOrRoles(endpoint))
            .Concat(WithInlinePolicies(endpoint))
            .Concat(WithUnknownPolicies(endpoint))
            .Concat(WithSeveralRolePolicies(endpoint)));
    }

    /// <summary>
    /// Every endpoint relies on the fallback policy alone, or is anonymous and authorized at once (the anonymous one
    /// wins, silently). An inline policy counts as none here: endpoints name a policy, and
    /// <see cref="WithInlinePolicies"/> reports one.
    /// </summary>
    private static IEnumerable<string> WithoutExplicitAuthorization(IEnumerable<RouteEndpoint> endpoints) =>
        endpoints.Where(endpoint => IsAnonymous(endpoint) == AuthorizeData(endpoint).Any()).SelectMany(Routes);

    private static IEnumerable<string> AnonymousNotAllowListed(IEnumerable<RouteEndpoint> endpoints) =>
        endpoints.Where(IsAnonymous).SelectMany(Routes).Except(_anonymous, StringComparer.Ordinal);

    /// <summary>
    /// Every member of the organization passes plain <c>RequireAuthorization()</c>: the endpoint layer would check
    /// nothing.
    /// </summary>
    private static IEnumerable<string> OrganizationEndpointsWithoutRolePolicy(IEnumerable<RouteEndpoint> endpoints) =>
        endpoints.Where(RequiresOrganization).Where(endpoint => !HasRolePolicy(endpoint)).SelectMany(Routes);

    /// <summary>Without an organization context there are no roles, so a role policy would refuse everyone.</summary>
    private static IEnumerable<string> OrganizationLessEndpointsWithRolePolicy(IEnumerable<RouteEndpoint> endpoints) =>
        endpoints.Where(endpoint => !RequiresOrganization(endpoint)).Where(HasRolePolicy).SelectMany(Routes);

    /// <summary>
    /// Schemes would rebuild the user without the organization's identity; roles would be read from the principal,
    /// not from <c>ICurrentUser</c>, and Admin wouldn't pass a Manager check.
    /// </summary>
    private static IEnumerable<string> WithSchemesOrRoles(IEnumerable<RouteEndpoint> endpoints) =>
        endpoints
            .Where(endpoint => AuthorizeData(endpoint).Any(data => data.AuthenticationSchemes is not null || data.Roles is not null))
            .SelectMany(Routes);

    /// <summary>
    /// The middleware combines inline policies and requirements with the named ones, and none of the checks above reads
    /// them: they could require a role from the principal or set schemes.
    /// </summary>
    private static IEnumerable<string> WithInlinePolicies(IEnumerable<RouteEndpoint> endpoints) =>
        endpoints
            .Where(endpoint => endpoint.Metadata.Any(item => item is AuthorizationPolicy or IAuthorizationRequirementData))
            .SelectMany(Routes);

    /// <summary>
    /// <see cref="Policies_SetNoSchemes"/> checks the catalog's policies, so a policy registered anywhere else goes
    /// unchecked.
    /// </summary>
    private static IEnumerable<string> WithUnknownPolicies(IEnumerable<RouteEndpoint> endpoints) =>
        endpoints
            .Where(endpoint => AuthorizeData(endpoint).Any(data => data.Policy is not null && !IsRolePolicy(data)))
            .SelectMany(Routes);

    /// <summary>
    /// Policies combine with AND, not "the last wins": a group's role policy and its endpoint's must both pass, and
    /// may share no role.
    /// </summary>
    private static IEnumerable<string> WithSeveralRolePolicies(IEnumerable<RouteEndpoint> endpoints) =>
        endpoints.Where(endpoint => AuthorizeData(endpoint).Count(IsRolePolicy) > 1).SelectMany(Routes);

    /// <summary>The host's endpoints, without the test modules.</summary>
    private IEnumerable<RouteEndpoint> HostEndpoints() => api.Services
        .GetRequiredService<EndpointDataSource>()
        .Endpoints
        .OfType<RouteEndpoint>()
        .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith(TestModules, StringComparison.Ordinal) != true);

    /// <summary>The endpoints under <c>/api/v1</c>, without the test modules.</summary>
    private IEnumerable<RouteEndpoint> ApiEndpoints() => HostEndpoints()
        .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith($"{EndpointModuleExtensions.ApiRoot}/", StringComparison.Ordinal) == true);

    // As the middleware decides: the last metadata wins, and an endpoint without any needs no context
    private static bool RequiresOrganization(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetMetadata<OrganizationContextMetadata>() is { IsRequired: true };

    private static bool IsAnonymous(RouteEndpoint endpoint) => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;

    private static IReadOnlyList<IAuthorizeData> AuthorizeData(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();

    private static bool HasRolePolicy(RouteEndpoint endpoint) => AuthorizeData(endpoint).Any(IsRolePolicy);

    private static bool IsRolePolicy(IAuthorizeData data) => data.Policy is { } policy && Policies.Roles.ContainsKey(policy);

    /// <summary>
    /// <c>"GET /api/v1/me"</c>, one per method, or <c>"*"</c> for any. A module's <c>""</c> endpoint has a trailing slash
    /// in its raw text, which its route doesn't need.
    /// </summary>
    private static IEnumerable<string> Routes(RouteEndpoint endpoint) =>
        (endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? ["*"])
        .Select(method => $"{method} {endpoint.RoutePattern.RawText?.TrimEnd('/')}");

    /// <summary>A <c>GET</c> endpoint with <paramref name="metadata"/>, for the negative controls.</summary>
    private static RouteEndpoint Endpoint(string pattern, params object[] metadata)
    {
        var builder = new RouteEndpointBuilder(_ => Task.CompletedTask, RoutePatternFactory.Parse(pattern), order: 0);
        builder.Metadata.Add(new HttpMethodMetadata([HttpMethods.Get]));
        foreach (var item in metadata)
        {
            builder.Metadata.Add(item);
        }

        return (RouteEndpoint)builder.Build();
    }

    /// <summary>An attribute's requirements, as an <see cref="IAuthorizationRequirementData"/> attribute adds them.</summary>
    private sealed class ManagerRequirementData : IAuthorizationRequirementData
    {
        public IEnumerable<IAuthorizationRequirement> GetRequirements() => [new RolesAuthorizationRequirement(["Manager"])];
    }
}
