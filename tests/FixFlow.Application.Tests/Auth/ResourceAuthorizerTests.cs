using FixFlow.Application.Auth;
using FixFlow.Domain.Organizations;
using Microsoft.Extensions.DependencyInjection;

namespace FixFlow.Application.Tests.Auth;

/// <summary>
/// <see cref="IResourceAuthorizer"/> as <c>AddFixFlowApplication</c> registers it, over this assembly's
/// <see cref="SampleAuthorizationHandler"/>: when it asks the resource's handler, and when it refuses without asking.
/// </summary>
public sealed class ResourceAuthorizerTests : IAsyncDisposable
{
    // Each test's scope and provider, disposed in reverse order
    private readonly Stack<IAsyncDisposable> _disposables = new();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task IsAllowed_HandlerAllows_IsTrue()
    {
        var owner = Member(Role.Tenant);
        var services = Build(owner);

        Assert.True(await Authorizer(services).IsAllowedAsync(new Sample(owner.UserId!.Value), SampleOps.Edit, _ct));
        Assert.Equal([SampleOps.Edit], services.GetRequiredService<AskedOperations>());
    }

    [Fact]
    public async Task IsAllowed_HandlerDenies_IsFalse()
    {
        var services = Build(Member(Role.Tenant));

        Assert.False(await Authorizer(services).IsAllowedAsync(new Sample(Guid.CreateVersion7()), SampleOps.Edit, _ct));
    }

    [Fact]
    public async Task IsAllowed_OperationTheHandlerDoesntKnow_IsFalse()
    {
        var manager = Member(Role.Manager);
        var services = Build(manager);

        Assert.False(await Authorizer(services).IsAllowedAsync(new Sample(manager.UserId!.Value), SampleOps.Archive, _ct));
    }

    [Fact]
    public async Task IsAllowed_NoOrganizationContext_IsFalseWithoutAskingTheHandler()
    {
        var userId = Guid.CreateVersion7();
        var services = Build(new TestCurrentUser(userId, OrganizationId: null, new HashSet<Role> { Role.Manager }));

        Assert.False(await Authorizer(services).IsAllowedAsync(new Sample(userId), SampleOps.Edit, _ct));
        Assert.Empty(services.GetRequiredService<AskedOperations>());
    }

    [Fact]
    public async Task IsAllowed_InATrustedScope_IsFalseWithoutAskingTheHandler()
    {
        // A trusted scope gives an organization and no roles (plan P25)
        var userId = Guid.CreateVersion7();
        var services = Build(new TestCurrentUser(userId, Guid.CreateVersion7(), new HashSet<Role>()));

        Assert.False(await Authorizer(services).IsAllowedAsync(new Sample(userId), SampleOps.Edit, _ct));
        Assert.Empty(services.GetRequiredService<AskedOperations>());
    }

    [Fact]
    public async Task IsAllowed_ResourceWithoutAHandler_Throws()
    {
        var services = Build(Member(Role.Admin));

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await Authorizer(services).IsAllowedAsync(new Unguarded(), SampleOps.Edit, _ct));
    }

    [Fact]
    public async Task IsAllowed_ResourceWithTwoHandlers_Throws()
    {
        var owner = Member(Role.Tenant);
        var services = Build(owner, extra => extra.AddScoped<IResourceAuthorizationHandler<Sample>>(
            provider => ActivatorUtilities.CreateInstance<SampleAuthorizationHandler>(provider)));

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await Authorizer(services).IsAllowedAsync(new Sample(owner.UserId!.Value), SampleOps.Edit, _ct));
    }

    [Fact]
    public void AddResourceAuthorizationHandlers_CalledAgain_RegistersEachHandlerOnce()
    {
        var services = new ServiceCollection()
            .AddResourceAuthorizationHandlers(typeof(ResourceAuthorizerTests).Assembly)
            .AddResourceAuthorizationHandlers(typeof(ResourceAuthorizerTests).Assembly);

        var registered = Assert.Single(services, s => s.ServiceType == typeof(IResourceAuthorizationHandler<Sample>));
        Assert.Equal(typeof(SampleAuthorizationHandler), registered.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, registered.Lifetime);
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

    private static IResourceAuthorizer Authorizer(IServiceProvider services) =>
        services.GetRequiredService<IResourceAuthorizer>();

    /// <summary>A request's services: the application's, with this assembly's handlers and the caller.</summary>
    private IServiceProvider Build(ICurrentUser currentUser, Action<IServiceCollection>? extra = null)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddFixFlowApplication()
            .AddResourceAuthorizationHandlers(typeof(ResourceAuthorizerTests).Assembly)
            .AddSingleton(currentUser)
            .AddSingleton<AskedOperations>();
        extra?.Invoke(services);

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var scope = provider.CreateAsyncScope();
        _disposables.Push(provider);
        _disposables.Push(scope);
        return scope.ServiceProvider;
    }

    private sealed record TestCurrentUser(Guid? UserId, Guid? OrganizationId, IReadOnlySet<Role> Roles) : ICurrentUser;
}

/// <summary>A record its owner may edit. Nobody may archive it: the handler doesn't know that operation.</summary>
public sealed record Sample(Guid OwnerId);

/// <summary>A record no handler covers.</summary>
public sealed record Unguarded;

public static class SampleOps
{
    public static readonly Operation Edit = new("Sample.Edit");

    public static readonly Operation Archive = new("Sample.Archive");
}

/// <summary>The operations the handler was asked about, in order.</summary>
public sealed class AskedOperations : List<Operation>;

internal sealed class SampleAuthorizationHandler(ICurrentUser currentUser, AskedOperations asked)
    : IResourceAuthorizationHandler<Sample>
{
    public ValueTask<bool> IsAllowedAsync(Sample resource, Operation operation, CancellationToken cancellationToken)
    {
        asked.Add(operation);
        return ValueTask.FromResult(operation == SampleOps.Edit && resource.OwnerId == currentUser.UserId);
    }
}
