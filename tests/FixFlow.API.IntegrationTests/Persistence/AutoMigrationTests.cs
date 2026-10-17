using FixFlow.API.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Npgsql;

namespace FixFlow.API.IntegrationTests.Persistence;

public class AutoMigrationTests(FixFlowApiFactory api)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("Development", true)]
    [InlineData("Testing", false)]
    [InlineData("Production", false)]
    public async Task Startup_MigratesOnlyInDevelopment(string environment, bool migrates)
    {
        var connectionString = await api.CreateDatabaseAsync();
        await using var app = api.WithWebHostBuilder(builder => builder
            .UseEnvironment(environment)
            .UseSetting("ConnectionStrings:FixFlow", connectionString)
            // Development's appsettings point at the local Seq: keep this host's telemetry out of it
            .UseSetting("Seq:ServerUrl", "")
            .UseSetting("Otlp:Endpoint", ""));

        // Runs Program up to RunAsync
        app.StartServer();

        Assert.Equal(migrates, await HasMigrationsHistoryAsync(connectionString));
    }

    private async Task<bool> HasMigrationsHistoryAsync(string connectionString)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var command = dataSource.CreateCommand(
            """SELECT to_regclass('public."__EFMigrationsHistory"') IS NOT NULL""");
        return (bool)(await command.ExecuteScalarAsync(_ct))!;
    }
}
