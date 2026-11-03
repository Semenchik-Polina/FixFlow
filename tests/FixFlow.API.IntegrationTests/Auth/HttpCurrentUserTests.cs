using System.Security.Claims;
using FixFlow.API.Auth;
using FixFlow.API.Organizations;
using FixFlow.Application.Auth;
using FixFlow.Domain.Organizations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FixFlow.API.IntegrationTests.Auth;

/// <summary>
/// <see cref="ICurrentUser"/> as <see cref="AuthExtensions.AddFixFlowAuth"/> registers it, over the framework's
/// <see cref="IHttpContextAccessor"/> and without a host. The accessor reads an AsyncLocal, which work queued without
/// the caller's ExecutionContext doesn't see: HybridCache runs its factories that way, and they query through the
/// current user.
/// </summary>
public sealed class HttpCurrentUserTests : IAsyncDisposable
{
    private readonly ServiceProvider _services = new ServiceCollection().AddFixFlowAuth().BuildServiceProvider();

    [Fact]
    public async Task UserId_ReadWithoutTheCallersExecutionContext_IsTheRequestsUser()
    {
        var userId = Guid.CreateVersion7();
        var accessor = _services.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext { User = SignedIn(userId) };
        await using var request = _services.CreateAsyncScope();
        var currentUser = request.ServiceProvider.GetRequiredService<ICurrentUser>();

        Task<HttpContext?> accessorElsewhere;
        Task<Guid?> userIdElsewhere;
        using (ExecutionContext.SuppressFlow())
        {
            accessorElsewhere = Task.Run<HttpContext?>(() => accessor.HttpContext);
            userIdElsewhere = Task.Run(() => currentUser.UserId);
        }

        // The accessor can't see the request there, so the current user must have kept it
        Assert.Null(await accessorElsewhere);
        Assert.Equal(userId, await userIdElsewhere);
    }

    [Fact]
    public async Task UserId_PrincipalReplacedAfterwards_IsTheNewUser()
    {
        var accessor = _services.GetRequiredService<IHttpContextAccessor>();
        var context = new DefaultHttpContext();
        accessor.HttpContext = context;
        await using var request = _services.CreateAsyncScope();
        var currentUser = request.ServiceProvider.GetRequiredService<ICurrentUser>();
        Assert.Null(currentUser.UserId);
        var userId = Guid.CreateVersion7();

        // Authentication sets the principal after services may already exist; it keeps the request, not its user
        context.User = SignedIn(userId);

        Assert.Equal(userId, currentUser.UserId);
    }

    [Fact]
    public async Task CurrentUser_ReadAfterTheRequestsScopeEnded_HasNoUserOrganizationOrRoles()
    {
        var accessor = _services.GetRequiredService<IHttpContextAccessor>();
        var context = new DefaultHttpContext { User = SignedIn(Guid.CreateVersion7()) };
        accessor.HttpContext = context;
        var request = _services.CreateAsyncScope();
        var currentUser = request.ServiceProvider.GetRequiredService<ICurrentUser>();
        Assert.NotNull(currentUser.UserId);

        await request.DisposeAsync();
        // Kestrel reuses the HttpContext for the connection's next request, which may be another user's
        context.User = InOrganization(Guid.CreateVersion7(), Guid.CreateVersion7(), nameof(Role.Admin));

        Assert.Null(currentUser.UserId);
        Assert.Null(currentUser.OrganizationId);
        Assert.Empty(currentUser.Roles);
    }

    [Fact]
    public async Task Roles_SignedInWithoutOrganization_AreEmpty()
    {
        var accessor = _services.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext { User = SignedIn(Guid.CreateVersion7()) };
        await using var request = _services.CreateAsyncScope();

        Assert.Empty(request.ServiceProvider.GetRequiredService<ICurrentUser>().Roles);
    }

    [Fact]
    public async Task Roles_RoleNames_AreTheRoles()
    {
        var accessor = _services.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext
        {
            User = InOrganization(Guid.CreateVersion7(), Guid.CreateVersion7(), nameof(Role.Tenant), nameof(Role.Manager)),
        };
        await using var request = _services.CreateAsyncScope();

        Assert.Equal(
            [Role.Tenant, Role.Manager],
            request.ServiceProvider.GetRequiredService<ICurrentUser>().Roles.Order());
    }

    [Theory]
    [InlineData("3")] // Admin's number
    [InlineData("Technician, Manager")] // flags that add up to Admin
    [InlineData(" Admin")]
    [InlineData("admin")]
    [InlineData("Owner")]
    public async Task Roles_ClaimThatIsNoRolesName_GrantsNothing(string value)
    {
        var accessor = _services.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext
        {
            User = InOrganization(Guid.CreateVersion7(), Guid.CreateVersion7(), value),
        };
        await using var request = _services.CreateAsyncScope();

        Assert.Empty(request.ServiceProvider.GetRequiredService<ICurrentUser>().Roles);
    }

    [Fact]
    public async Task CurrentUser_OrganizationClaimsOnTheCookiesIdentity_GiveNoOrganizationOrRoles()
    {
        // The claims stored with an account reach the cookie's identity (Identity's user claims)
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, Guid.CreateVersion7().ToString()),
                new Claim(FixFlowClaimTypes.OrganizationId, Guid.CreateVersion7().ToString()),
                new Claim(FixFlowClaimTypes.Role, nameof(Role.Admin)),
            ],
            "Identity.Application"));
        var accessor = _services.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext { User = principal };
        await using var request = _services.CreateAsyncScope();
        var currentUser = request.ServiceProvider.GetRequiredService<ICurrentUser>();

        Assert.Null(currentUser.OrganizationId);
        Assert.Empty(currentUser.Roles);
    }

    [Fact]
    public async Task CurrentUser_OrganizationClaimsOnBothIdentities_AreTheOrganizationContexts()
    {
        var organizationId = Guid.CreateVersion7();
        var principal = InOrganization(Guid.CreateVersion7(), organizationId, nameof(Role.Tenant));
        // Ahead of the context's identity, where a search of the whole principal would find them first
        ((ClaimsIdentity)principal.Identity!).AddClaims(
        [
            new Claim(FixFlowClaimTypes.OrganizationId, Guid.CreateVersion7().ToString()),
            new Claim(FixFlowClaimTypes.Role, nameof(Role.Admin)),
        ]);
        var accessor = _services.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext { User = principal };
        await using var request = _services.CreateAsyncScope();
        var currentUser = request.ServiceProvider.GetRequiredService<ICurrentUser>();

        Assert.Equal(organizationId, currentUser.OrganizationId);
        Assert.Equal([Role.Tenant], currentUser.Roles);
    }

    [Fact]
    public async Task CurrentUser_TwoOrganizationContextIdentities_GiveNoOrganizationOrRoles()
    {
        var principal = InOrganization(Guid.CreateVersion7(), Guid.CreateVersion7(), nameof(Role.Tenant));
        // A second one, stale or foreign: neither may decide
        principal.AddIdentity(ContextIdentity(Guid.CreateVersion7(), nameof(Role.Admin)));
        var accessor = _services.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext { User = principal };
        await using var request = _services.CreateAsyncScope();
        var currentUser = request.ServiceProvider.GetRequiredService<ICurrentUser>();

        Assert.Null(currentUser.OrganizationId);
        Assert.Empty(currentUser.Roles);
    }

    [Fact]
    public async Task UserId_NoRequest_IsNull()
    {
        await using var scope = _services.CreateAsyncScope();

        Assert.Null(scope.ServiceProvider.GetRequiredService<ICurrentUser>().UserId);
    }

    public ValueTask DisposeAsync() => _services.DisposeAsync();

    private static ClaimsPrincipal SignedIn(Guid userId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "Test"));

    /// <summary>Signed in, with an organization context's identity as the middleware adds it.</summary>
    private static ClaimsPrincipal InOrganization(Guid userId, Guid organizationId, params string[] roles)
    {
        var principal = SignedIn(userId);
        principal.AddIdentity(ContextIdentity(organizationId, roles));
        return principal;
    }

    private static ClaimsIdentity ContextIdentity(Guid organizationId, params string[] roles) => new(
        [
            new Claim(FixFlowClaimTypes.OrganizationId, organizationId.ToString()),
            .. roles.Select(role => new Claim(FixFlowClaimTypes.Role, role)),
        ],
        OrganizationContext.AuthenticationType,
        nameType: null,
        roleType: FixFlowClaimTypes.Role);
}
