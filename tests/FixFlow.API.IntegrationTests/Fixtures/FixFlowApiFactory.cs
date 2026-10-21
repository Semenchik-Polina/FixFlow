using FixFlow.API.Endpoints;
using FixFlow.API.IntegrationTests.Conventions;
using FixFlow.API.IntegrationTests.Persistence;
using FixFlow.Application.Email;
using FixFlow.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NodaTime;
using NodaTime.Testing;
using Serilog.Core;

namespace FixFlow.API.IntegrationTests.Fixtures;

/// <summary>
/// The API host shared by the whole run (an assembly fixture): test classes take it through their constructor.
/// Tests run in parallel against it, so they never change shared state: each test creates its own data
/// (<see cref="TestData"/>) and reads the sinks only by its own unique values. The clock is frozen at
/// <see cref="SharedNow"/>; a test that moves time uses <see cref="WithOwnClock"/>.
/// </summary>
public class FixFlowApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>A Wednesday, weeks away from any DST change in either hemisphere.</summary>
    public static Instant SharedNow { get; } = Instant.FromUtc(2026, 1, 14, 9, 0);

    private readonly TestInfrastructure _infrastructure;
    private readonly bool _ownsInfrastructure;
    private readonly FakeClock _clock;
    private PersistenceTestDatabase? _persistence;

    public FixFlowApiFactory()
        : this(new TestInfrastructure(), ownsInfrastructure: true, new FakeClock(SharedNow))
    {
    }

    private protected FixFlowApiFactory(TestInfrastructure infrastructure, bool ownsInfrastructure, FakeClock clock)
    {
        _infrastructure = infrastructure;
        _ownsInfrastructure = ownsInfrastructure;
        _clock = clock;
    }

    /// <summary>The app's <see cref="IClock"/>. Read-only here: advancing it would leak into parallel tests.</summary>
    public IClock Clock => _clock;

    /// <summary>Mail the app sent. Read it only through your own recipient.</summary>
    public InMemoryEmailSink Emails { get; } = new();

    /// <summary>The app's log events, after enrichment and masking.</summary>
    public TestLogSink Logs { get; } = new();

    /// <summary>
    /// A separate host on the same containers, with its own clock and sinks, for a test that moves time.
    /// Dispose it with <c>await using</c>.
    /// </summary>
    public OwnClockApiFactory WithOwnClock(Instant start) => new(_infrastructure, new FakeClock(start));

    /// <summary>
    /// A database of its own with test-only entities, for tests of the persistence conventions. Only on the shared
    /// factory.
    /// </summary>
    public PersistenceTestDatabase Persistence =>
        _persistence ?? throw new InvalidOperationException("Use the shared FixFlowApiFactory for persistence tests.");

    /// <summary>A new, empty database on the run's PostgreSQL. Returns the app's connection string to it.</summary>
    public Task<string> CreateDatabaseAsync() =>
        _infrastructure.CreateDatabaseAsync($"fixflow_{Guid.CreateVersion7():N}", TestContext.Current.CancellationToken);

    public async ValueTask InitializeAsync()
    {
        if (_ownsInfrastructure)
        {
            await _infrastructure.StartAsync();
        }

        // Build the host now: parallel tests would otherwise race to build it on first use
        StartServer();

        if (_ownsInfrastructure)
        {
            // Once per run, before any test: the app migrates on startup in Development only
            await Services.MigrateFixFlowDatabaseAsync();
            _persistence = await PersistenceTestDatabase.CreateAsync(_infrastructure);
        }
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        if (_persistence is not null)
        {
            await _persistence.DisposeAsync();
        }

        if (_ownsInfrastructure)
        {
            await _infrastructure.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder
            // No user-secrets, Seq or OTLP: those come only with Development
            .UseEnvironment("Testing")
            // Program reads these before Build(), so they go in as host settings
            .UseSetting("ConnectionStrings:FixFlow", _infrastructure.AppConnectionString)
            .UseSetting("Database:Password", _infrastructure.AppPassword)
            .UseSetting("ConnectionStrings:Blob", _infrastructure.BlobConnectionString)
            // Runs after the app's registrations, so these replace the real clock and sender
            .ConfigureTestServices(services =>
            {
                services.RemoveAll<IClock>();
                services.AddSingleton<IClock>(_clock);
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(Emails);
                services.AddSingleton<ILogEventSink>(Logs);

                // Test-only endpoints with their handlers and validators, behind the app's own pipeline behaviours
                services.AddMediatR(config => config.RegisterServicesFromAssemblyContaining<FixFlowApiFactory>());
                services.AddValidatorsFromAssemblyContaining<FixFlowApiFactory>(includeInternalTypes: true);
                services.AddEndpointModule<ConventionTestEndpoints>();
            });
    }
}
