using System.Security.Claims;
using FixFlow.API.Auth;
using FixFlow.Application.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FixFlow.API.IntegrationTests.Auth;

/// <summary>
/// The trusted organization scope as <see cref="AuthExtensions.AddFixFlowAuth"/> registers it, over a
/// <see cref="DefaultHttpContext"/> and without a host: the handler that opens it and <see cref="ICurrentUser"/> share
/// one scope object per request.
/// </summary>
public sealed class TrustedOrganizationScopeTests : IAsyncDisposable
{
    private readonly FixedHttpContextAccessor _httpContext = new();
    private readonly ServiceProvider _services;

    public TrustedOrganizationScopeTests()
    {
        _services = new ServiceCollection()
            .AddFixFlowAuth()
            .AddSingleton<IHttpContextAccessor>(_httpContext)
            .BuildServiceProvider();
    }

    [Fact]
    public async Task Open_NoContext_CurrentUserHasTheOrganization()
    {
        await using var request = _services.CreateAsyncScope();
        var organizationId = Guid.CreateVersion7();

        using var scope = TrustedScope(request).Open(organizationId);

        Assert.Equal(organizationId, CurrentUser(request).OrganizationId);
    }

    [Fact]
    public async Task Open_SignedInWithoutContext_CurrentUserHasNoRoles()
    {
        _httpContext.HttpContext!.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.CreateVersion7().ToString())], authenticationType: "Test"));
        await using var request = _services.CreateAsyncScope();

        using var scope = TrustedScope(request).Open(Guid.CreateVersion7());

        // The scope gives an organization, never roles: no role check passes inside it
        Assert.NotNull(CurrentUser(request).OrganizationId);
        Assert.Empty(CurrentUser(request).Roles);
    }

    [Fact]
    public async Task Dispose_OpenScope_CurrentUserHasNoOrganization()
    {
        await using var request = _services.CreateAsyncScope();

        TrustedScope(request).Open(Guid.CreateVersion7()).Dispose();

        Assert.Null(CurrentUser(request).OrganizationId);
    }

    [Fact]
    public async Task Dispose_Twice_LeavesALaterScopeOpen()
    {
        await using var request = _services.CreateAsyncScope();
        var first = TrustedScope(request).Open(Guid.CreateVersion7());
        first.Dispose();
        var laterId = Guid.CreateVersion7();
        using var later = TrustedScope(request).Open(laterId);

        first.Dispose();

        Assert.Equal(laterId, CurrentUser(request).OrganizationId);
    }

    [Fact]
    public async Task Open_ScopeAlreadyOpen_Throws()
    {
        await using var request = _services.CreateAsyncScope();
        var openId = Guid.CreateVersion7();
        using var open = TrustedScope(request).Open(openId);

        Assert.Throws<InvalidOperationException>(() => TrustedScope(request).Open(Guid.CreateVersion7()));
        Assert.Equal(openId, CurrentUser(request).OrganizationId);
    }

    [Fact]
    public async Task Open_RequestHasOrganizationClaim_Throws()
    {
        var claimedId = Guid.CreateVersion7();
        _httpContext.HttpContext!.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(FixFlowClaimTypes.OrganizationId, claimedId.ToString())], authenticationType: "Test"));
        await using var request = _services.CreateAsyncScope();

        Assert.Throws<InvalidOperationException>(() => TrustedScope(request).Open(Guid.CreateVersion7()));
        Assert.Equal(claimedId, CurrentUser(request).OrganizationId);
    }

    [Fact]
    public async Task Open_EmptyId_Throws()
    {
        await using var request = _services.CreateAsyncScope();

        Assert.Throws<InvalidOperationException>(() => TrustedScope(request).Open(Guid.Empty));
        Assert.Null(CurrentUser(request).OrganizationId);
    }

    [Fact]
    public async Task Open_AnotherRequest_HasNoOrganization()
    {
        await using var request = _services.CreateAsyncScope();
        await using var another = _services.CreateAsyncScope();

        using var scope = TrustedScope(request).Open(Guid.CreateVersion7());

        Assert.Null(CurrentUser(another).OrganizationId);
    }

    [Fact]
    public async Task CurrentUser_CastToTheScope_IsNotPossible()
    {
        await using var request = _services.CreateAsyncScope();

        Assert.IsNotAssignableFrom<ITrustedOrganizationScope>(CurrentUser(request));
    }

    public ValueTask DisposeAsync() => _services.DisposeAsync();

    private static ITrustedOrganizationScope TrustedScope(AsyncServiceScope request) =>
        request.ServiceProvider.GetRequiredService<ITrustedOrganizationScope>();

    private static ICurrentUser CurrentUser(AsyncServiceScope request) =>
        request.ServiceProvider.GetRequiredService<ICurrentUser>();

    /// <summary>
    /// One request's context. The framework's accessor keeps it in an <c>AsyncLocal</c>, which a test would have to set
    /// on its own async flow.
    /// </summary>
    private sealed class FixedHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = new DefaultHttpContext();
    }
}
