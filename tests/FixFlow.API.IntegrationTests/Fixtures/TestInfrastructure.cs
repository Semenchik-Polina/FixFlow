using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Npgsql;
using Testcontainers.Azurite;
using Testcontainers.PostgreSql;

namespace FixFlow.API.IntegrationTests.Fixtures;

/// <summary>
/// PostgreSQL and Azurite containers for one test run, on the Compose image tags. PostgreSQL runs the Compose init
/// script, so the app connects as the same non-superuser role that owns its database (plan A14). Passwords are
/// generated per run. Testcontainers' reaper removes the containers even if the test process dies.
/// </summary>
internal sealed partial class TestInfrastructure : IAsyncDisposable
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

    /// <summary>
    /// A new, empty database owned by the app role, like the one the init script creates. The app role can't create
    /// databases (plan A14), so the container's superuser does. Returns the app's connection string, password included.
    /// </summary>
    public async Task<string> CreateDatabaseAsync(string name, CancellationToken cancellationToken = default)
    {
        // An identifier can't be a parameter: accept only names the tests generate
        if (!TestDatabaseName().IsMatch(name))
        {
            throw new ArgumentException($"Not a test database name: {name}", nameof(name));
        }

        await using (var dataSource = NpgsqlDataSource.Create(_postgres.GetConnectionString()))
        await using (var command = dataSource.CreateCommand($"CREATE DATABASE {name} OWNER {AppUser}"))
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        return new NpgsqlConnectionStringBuilder(AppConnectionString)
        {
            Database = name,
            Password = AppPassword,
        }.ConnectionString;
    }

    public async ValueTask DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _azurite.DisposeAsync();
    }

    private static string NewPassword() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));

    [GeneratedRegex("^fixflow_[a-z0-9_]+$")]
    private static partial Regex TestDatabaseName();
}
