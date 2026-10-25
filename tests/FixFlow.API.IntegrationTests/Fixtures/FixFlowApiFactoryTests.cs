using System.Net;
using FixFlow.API.RateLimiting;
using FixFlow.Application.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Npgsql;
using Serilog.Events;

namespace FixFlow.API.IntegrationTests.Fixtures;

public class FixFlowApiFactoryTests(FixFlowApiFactory api)
{
    [Fact]
    public void Services_Clock_IsFrozenAtSharedNow()
    {
        var clock = api.Services.GetRequiredService<IClock>();

        Assert.Same(api.Clock, clock);
        Assert.Equal(FixFlowApiFactory.SharedNow, clock.GetCurrentInstant());
    }

    [Fact]
    public async Task WithOwnClock_Advance_MovesOnlyThatHostsClock()
    {
        var start = Instant.FromUtc(2026, 3, 29, 0, 30);
        await using var app = api.WithOwnClock(start);

        app.Clock.Advance(Duration.FromHours(2));

        Assert.Equal(start + Duration.FromHours(2), app.Services.GetRequiredService<IClock>().GetCurrentInstant());
        Assert.Equal(FixFlowApiFactory.SharedNow, api.Services.GetRequiredService<IClock>().GetCurrentInstant());
    }

    [Fact]
    public async Task Emails_SentThroughEmailSender_AreCapturedForRecipient()
    {
        var recipient = TestData.Email();
        var sender = api.Services.GetRequiredService<IEmailSender>();

        await sender.SendAsync(new EmailMessage(recipient, "Subject", "Body"), TestContext.Current.CancellationToken);

        var message = Assert.Single(api.Emails.To(recipient));
        Assert.Equal("Subject", message.Subject);
    }

    [Fact]
    public async Task Logs_Request_CapturesAppRequestEvent()
    {
        var path = $"/{TestData.OrganizationSlug()}";
        using var client = api.CreateApiClient();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        // The fallback policy answers unknown routes too
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(api.Logs.Events, logEvent =>
            logEvent.Properties.TryGetValue("RequestPath", out var value)
            && value is ScalarValue { Value: string requestPath }
            && requestPath == path);
    }

    [Fact]
    public void CreateApiClient_EachClient_SendsAnAddressInItsOwnSlash64()
    {
        using var first = api.CreateApiClient();
        using var second = api.CreateApiClient();

        var firstAddress = IPAddress.Parse(first.DefaultRequestHeaders
            .GetValues(WebApplicationFactoryExtensions.ClientAddressHeader).Single());
        var secondAddress = IPAddress.Parse(second.DefaultRequestHeaders
            .GetValues(WebApplicationFactoryExtensions.ClientAddressHeader).Single());

        Assert.NotEqual(
            RateLimitingExtensions.ClientAddressKey(firstAddress),
            RateLimitingExtensions.ClientAddressKey(secondAddress));
    }

    [Fact]
    public async Task ClientAddressHeader_SetsTheConnectionsAddress()
    {
        var address = WebApplicationFactoryExtensions.NextClientAddress();

        var withHeader = await api.Server.SendAsync(
            context => context.Request.Headers[WebApplicationFactoryExtensions.ClientAddressHeader] = address.ToString(),
            TestContext.Current.CancellationToken);
        var withoutHeader = await api.Server.SendAsync(_ => { }, TestContext.Current.CancellationToken);

        Assert.Equal(address, withHeader.Connection.RemoteIpAddress);
        // TestServer's own default, which every client would otherwise share
        Assert.Null(withoutHeader.Connection.RemoteIpAddress);
    }

    [Fact]
    public async Task Database_AppConnection_IsNotSuperuser()
    {
        // The app's own settings: the Compose init script created this role (plan A14)
        var configuration = api.Services.GetRequiredService<IConfiguration>();
        var connectionString = new NpgsqlConnectionStringBuilder(configuration.GetConnectionString("FixFlow"))
        {
            Password = configuration["Database:Password"],
        }.ConnectionString;
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var command = dataSource.CreateCommand(
            "SELECT current_user, rolsuper FROM pg_roles WHERE rolname = current_user");

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        Assert.True(await reader.ReadAsync(cancellationToken));
        Assert.Equal("fixflow_app", reader.GetString(0));
        Assert.False(reader.GetBoolean(1));
    }
}
