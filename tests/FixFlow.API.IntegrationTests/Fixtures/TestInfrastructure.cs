using System.Security.Cryptography;
using Npgsql;
using Testcontainers.Azurite;
using Testcontainers.PostgreSql;

namespace FixFlow.API.IntegrationTests.Fixtures;

/// <summary>
/// PostgreSQL and Azurite containers for one test run, on the Compose image tags. PostgreSQL runs the Compose init
/// script, so the app connects as the same non-superuser role that owns its database (plan A14). Passwords are
/// generated per run. Testcontainers' reaper removes the containers even if the test process dies.
/// </summary>
internal sealed class TestInfrastructure : IAsyncDisposable
{
    private const string DatabaseName = "fixflow";
    private const string AppUser = "fixflow_app";

    private readonly PostgreSqlContainer _postgres;
    private readonly AzuriteContainer _azurite;

    public TestInfrastructure()
    {
        _postgres = new PostgreSqlBuilder(ComposeImages.Get(ComposeImages.Postgres))
            .WithPassword(NewPassword())
            .WithEnvironment("FIXFLOW_DB_NAME", DatabaseName)
            .WithEnvironment("FIXFLOW_DB_USER", AppUser)
            .WithEnvironment("FIXFLOW_DB_PASSWORD", AppPassword)
            .WithResourceMapping(
                new DirectoryInfo(Path.Combine(ComposeImages.ComposeDirectory, "initdb")), "/docker-entrypoint-initdb.d/")
            .Build();

        // The flags Compose passes: the Blob SDK may send a newer API version than this Azurite knows
        _azurite = new AzuriteBuilder(ComposeImages.Get(ComposeImages.Azurite))
            .WithInMemoryPersistence()
            .WithCommand("--loose", "--skipApiVersionCheck", "--disableTelemetry")
            .Build();
    }

    /// <summary>The app's connection string without the password, like <c>ConnectionStrings:FixFlow</c>.</summary>
    public string AppConnectionString => new NpgsqlConnectionStringBuilder
    {
        Host = _postgres.Hostname,
        Port = _postgres.GetMappedPublicPort(PostgreSqlBuilder.PostgreSqlPort),
        Database = DatabaseName,
        Username = AppUser,
    }.ConnectionString;

    /// <summary>The app role's password, like <c>Database:Password</c>.</summary>
    public string AppPassword { get; } = NewPassword();

    public string BlobConnectionString => _azurite.GetConnectionString();

    public Task StartAsync(CancellationToken cancellationToken = default) =>
        Task.WhenAll(_postgres.StartAsync(cancellationToken), _azurite.StartAsync(cancellationToken));

    public async ValueTask DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _azurite.DisposeAsync();
    }

    private static string NewPassword() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
}
