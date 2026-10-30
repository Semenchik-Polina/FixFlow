using System.Security.Claims;
using FixFlow.API.Auth;
using FixFlow.Application.Auth;
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
    public async Task Roles_SignedInWithoutOrganization_AreEmpty()
    {
        var accessor = _services.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext { User = SignedIn(Guid.CreateVersion7()) };
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
}
