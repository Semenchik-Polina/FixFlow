using System.Security.Claims;
using FixFlow.API.Auth;
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
        principal.AddIdentity(new ClaimsIdentity(
            [
                new Claim(FixFlowClaimTypes.OrganizationId, organizationId.ToString()),
                .. roles.Select(role => new Claim(FixFlowClaimTypes.Role, role)),
            ],
            "Test.Organization",
            nameType: null,
            roleType: FixFlowClaimTypes.Role));
        return principal;
    }
}
