using System.Security.Cryptography;
using System.Text.RegularExpressions;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Npgsql;
using Testcontainers.Azurite;
using Testcontainers.PostgreSql;

namespace FixFlow.API.IntegrationTests.Fixtures;

/// <summary>
/// PostgreSQL, Azurite and Mailpit containers for one test run, on the Compose image tags. PostgreSQL runs the Compose
/// init script, so the app connects as the same non-superuser role that owns its database (plan A14). Passwords are
/// generated per run. Two Mailpits: one accepts every message, the other rejects every recipient (Mailpit's chaos
/// triggers apply to the whole instance, so never set any on the first). Testcontainers' reaper removes the containers
/// even if the test process dies.
/// </summary>
internal sealed partial class TestInfrastructure : IAsyncDisposable
{
    private const string DatabaseName = "fixflow";
    private const string AppUser = "fixflow_app";
    private const ushort MailpitSmtpPort = 1025;
    private const ushort MailpitHttpPort = 8025;

    private readonly PostgreSqlContainer _postgres;
    private readonly AzuriteContainer _azurite;
    private readonly IContainer _mailpit;
    private readonly IContainer _rejectingMailpit;
    private MailpitApi? _mailpitApi;

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

        _mailpit = MailpitBuilder().Build();
        // 550 to every RCPT TO
        _rejectingMailpit = MailpitBuilder().WithEnvironment("MP_CHAOS_TRIGGERS", "Recipient:550:100").Build();
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

    /// <summary>The Mailpit that accepts every message.</summary>
    public SmtpEndpoint MailpitSmtp => SmtpOf(_mailpit);

    /// <summary>The Mailpit that rejects every recipient with 550.</summary>
    public SmtpEndpoint RejectingMailpitSmtp => SmtpOf(_rejectingMailpit);

    /// <summary>The API of the Mailpit that accepts every message.</summary>
    public MailpitApi Mailpit => _mailpitApi ?? throw new InvalidOperationException("The containers aren't started.");

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await Task.WhenAll(
            _postgres.StartAsync(cancellationToken),
            _azurite.StartAsync(cancellationToken),
            _mailpit.StartAsync(cancellationToken),
            _rejectingMailpit.StartAsync(cancellationToken));

        // Once, here: parallel tests would otherwise race to create it on first use
        _mailpitApi = new MailpitApi(
            new UriBuilder(Uri.UriSchemeHttp, _mailpit.Hostname, _mailpit.GetMappedPublicPort(MailpitHttpPort)).Uri);
    }

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
        _mailpitApi?.Dispose();
        await _postgres.DisposeAsync();
        await _azurite.DisposeAsync();
        await _mailpit.DisposeAsync();
        await _rejectingMailpit.DisposeAsync();
    }

    private static ContainerBuilder MailpitBuilder() =>
        new ContainerBuilder(ComposeImages.Get(ComposeImages.Mailpit))
            .WithPortBinding(MailpitSmtpPort, assignRandomHostPort: true)
            .WithPortBinding(MailpitHttpPort, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(request => request.ForPort(MailpitHttpPort).ForPath("/readyz")));

    private static SmtpEndpoint SmtpOf(IContainer mailpit) =>
        new(mailpit.Hostname, mailpit.GetMappedPublicPort(MailpitSmtpPort));

    private static string NewPassword() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));

    [GeneratedRegex("^fixflow_[a-z0-9_]+$")]
    private static partial Regex TestDatabaseName();
}
