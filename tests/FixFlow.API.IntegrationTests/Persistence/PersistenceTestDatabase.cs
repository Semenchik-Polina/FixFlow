using FixFlow.API.IntegrationTests.Fixtures;
using FixFlow.API.IntegrationTests.Persistence.TestModel;
using FixFlow.Application;
using FixFlow.Application.Auth;
using FixFlow.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using NodaTime.Testing;
using Serilog;

namespace FixFlow.API.IntegrationTests.Persistence;

/// <summary>
/// A database of its own on the run's PostgreSQL, created once by <see cref="FixFlowApiFactory"/> from
/// <see cref="TestDbContext"/> (<c>EnsureCreated</c>), so the test entities never clash with the app's migrations.
/// Persistence is wired as in the app; each scope picks its caller and clock. Tests run in parallel against it, so
/// each works in its own organization (<see cref="TestCurrentUser.InNewOrganization"/>).
/// </summary>
public sealed class PersistenceTestDatabase : IAsyncDisposable
{
    private readonly ServiceProvider _services;

    private PersistenceTestDatabase(string connectionString)
    {
        var services = new ServiceCollection();

        services.AddLogging(logging => logging.AddSerilog(
            new LoggerConfiguration().MinimumLevel.Warning().WriteTo.Sink(Logs).CreateLogger(), dispose: true));
        services.AddSingleton<PostCommitRecorder>();
        services.AddScoped<ScopeSettings>();
        services.AddScoped<ICurrentUser>(provider => provider.GetRequiredService<ScopeSettings>().User);
        services.AddScoped<IClock>(provider => provider.GetRequiredService<ScopeSettings>().Clock);
        services.AddFixFlowPersistence<TestDbContext>(_ => connectionString);
        services.AddDomainEventHandlers(typeof(TestDbContext).Assembly);

        _services = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true,
        });
    }

    /// <summary>Warnings and errors logged through persistence, e.g. failed post-commit handlers.</summary>
    public TestLogSink Logs { get; } = new();

    public PostCommitRecorder PostCommit => _services.GetRequiredService<PostCommitRecorder>();

    internal static async Task<PersistenceTestDatabase> CreateAsync(TestInfrastructure infrastructure)
    {
        var connectionString = await infrastructure.CreateDatabaseAsync("fixflow_persistence");
        var database = new PersistenceTestDatabase(connectionString);

        await using var scope = database.CreateScope(TestCurrentUser.WithoutOrganization());
        await scope.Db.Database.EnsureCreatedAsync();

        return database;
    }

    /// <summary>
    /// One unit of work (like one request) as <paramref name="user"/>, at <paramref name="clock"/>'s time
    /// (<see cref="FixFlowApiFactory.SharedNow"/> by default).
    /// </summary>
    public PersistenceTestScope CreateScope(ICurrentUser user, IClock? clock = null)
    {
        var scope = _services.CreateAsyncScope();
        var settings = scope.ServiceProvider.GetRequiredService<ScopeSettings>();
        settings.User = user;
        settings.Clock = clock ?? new FakeClock(FixFlowApiFactory.SharedNow);
        return new PersistenceTestScope(scope);
    }

    public ValueTask DisposeAsync() => _services.DisposeAsync();

    /// <summary>Set by <see cref="CreateScope"/> before anything in the scope is resolved.</summary>
    private sealed class ScopeSettings
    {
        public ICurrentUser User { get; set; } = null!;

        public IClock Clock { get; set; } = null!;
    }
}

public sealed class PersistenceTestScope(AsyncServiceScope scope) : IAsyncDisposable
{
    public TestDbContext Db => scope.ServiceProvider.GetRequiredService<TestDbContext>();

    public ValueTask DisposeAsync() => scope.DisposeAsync();
}
