using FixFlow.Application.Auth;
using FixFlow.Application.Properties;
using FixFlow.Domain.Organizations;
using FixFlow.Domain.Properties;
using Microsoft.Extensions.DependencyInjection;

namespace FixFlow.Application.Tests.Properties;

/// <summary>
/// The record checks on properties and units, through <see cref="IResourceAuthorizer"/> as <c>AddFixFlowApplication</c>
/// registers it, and the <c>VisibleTo</c> filters that go with them: Managers and Admins only.
/// </summary>
public sealed class PropertyAuthorizationTests : IAsyncDisposable
{
    private static readonly Address _address = Address.Create("Main Street 1", null, "Springfield", null, "PL");

    // Each test's scope and provider, disposed in reverse order
    private readonly Stack<IAsyncDisposable> _disposables = new();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(Role.Tenant, false)]
    [InlineData(Role.Technician, false)]
    [InlineData(Role.Manager, true)]
    [InlineData(Role.Admin, true)]
    public async Task IsAllowed_EveryPropertyOperation_OnlyForManagersAndAdmins(Role role, bool allowed)
    {
        var authorizer = Authorizer(Member(role));
        var property = Property.Create("Riverside", _address, "Europe/Warsaw");

        foreach (var operation in new[] { PropertyOps.Update, PropertyOps.Archive, PropertyOps.AddUnit })
        {
            Assert.Equal(allowed, await authorizer.IsAllowedAsync(property, operation, _ct));
        }
    }

    [Theory]
    [InlineData(Role.Tenant, false)]
    [InlineData(Role.Technician, false)]
    [InlineData(Role.Manager, true)]
    [InlineData(Role.Admin, true)]
    public async Task IsAllowed_EveryUnitOperation_OnlyForManagersAndAdmins(Role role, bool allowed)
    {
        var authorizer = Authorizer(Member(role));
        var unit = Unit.Create(Guid.CreateVersion7(), "4B");

        foreach (var operation in new[] { UnitOps.Rename, UnitOps.Archive })
        {
            Assert.Equal(allowed, await authorizer.IsAllowedAsync(unit, operation, _ct));
        }
    }

    [Fact]
    public async Task IsAllowed_OperationTheHandlerDoesntKnow_IsFalseEvenForAnAdmin()
    {
        var authorizer = Authorizer(Member(Role.Admin));

        Assert.False(await authorizer.IsAllowedAsync(
            Property.Create("Riverside", _address, "Europe/Warsaw"), UnitOps.Archive, _ct));
        Assert.False(await authorizer.IsAllowedAsync(Unit.Create(Guid.CreateVersion7(), "4B"), PropertyOps.AddUnit, _ct));
    }

    [Theory]
    [InlineData(Role.Tenant, 0)]
    [InlineData(Role.Technician, 0)]
    [InlineData(Role.Manager, 2)]
    [InlineData(Role.Admin, 2)]
    public void VisibleTo_ByRole_AllToManagersAndAdminsNoneToOthers(Role role, int visible)
    {
        var properties = new[]
        {
            Property.Create("Riverside", _address, "Europe/Warsaw"),
            Property.Create("Harbour View", _address, "Europe/Warsaw"),
        }.AsQueryable();
        var propertyId = Guid.CreateVersion7();
        var units = new[] { Unit.Create(propertyId, "4A"), Unit.Create(propertyId, "4B") }.AsQueryable();

        Assert.Equal(visible, properties.VisibleTo(Member(role)).Count());
        Assert.Equal(visible, units.VisibleTo(Member(role)).Count());
    }

    [Fact]
    public void VisibleTo_NoRoles_ShowsNothing()
    {
        // An open trusted scope gives an organization and no roles (plan P25)
        var user = new TestCurrentUser(Guid.CreateVersion7(), Guid.CreateVersion7(), new HashSet<Role>());

        Assert.Empty(new[] { Property.Create("Riverside", _address, "Europe/Warsaw") }.AsQueryable().VisibleTo(user));
        Assert.Empty(new[] { Unit.Create(Guid.CreateVersion7(), "4B") }.AsQueryable().VisibleTo(user));
    }

    public async ValueTask DisposeAsync()
    {
        while (_disposables.TryPop(out var disposable))
        {
            await disposable.DisposeAsync();
        }
    }

    private static TestCurrentUser Member(params Role[] roles) =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), roles.ToHashSet());

    /// <summary>A request's authorizer: the application's services, with the caller.</summary>
    private IResourceAuthorizer Authorizer(ICurrentUser currentUser)
    {
        var provider = new ServiceCollection()
            .AddLogging()
            .AddFixFlowApplication()
            .AddSingleton(currentUser)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var scope = provider.CreateAsyncScope();
        _disposables.Push(provider);
        _disposables.Push(scope);
        return scope.ServiceProvider.GetRequiredService<IResourceAuthorizer>();
    }

    private sealed record TestCurrentUser(Guid? UserId, Guid? OrganizationId, IReadOnlySet<Role> Roles) : ICurrentUser;
}
