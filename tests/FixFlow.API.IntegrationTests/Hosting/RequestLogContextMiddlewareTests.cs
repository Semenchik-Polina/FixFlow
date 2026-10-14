using System.Diagnostics;
using System.Security.Claims;
using FixFlow.API.IntegrationTests.Fixtures;
using FixFlow.Hosting.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Hosting;

namespace FixFlow.API.IntegrationTests.Hosting;

public class RequestLogContextMiddlewareTests
{
    private const string OrganizationClaimType = "test:organization_id";

    [Fact]
    public async Task InvokeAsync_UserAndOrganizationClaims_AddsIdsToLogsCompletionEventAndTrace()
    {
        var userId = Guid.CreateVersion7().ToString();
        var organizationId = Guid.CreateVersion7().ToString();
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(OrganizationClaimType, organizationId),
                new Claim(ClaimTypes.Email, "jane.doe@example.test"),
            ],
            "Test"));

        var (logged, completion, activity) = await InvokeAsync(user);

        Assert.Equal(["OrganizationId", "UserId"], logged.Properties.Keys.Order());
        Assert.Equal(userId, Scalar(logged.Properties["UserId"]));
        Assert.Equal(organizationId, Scalar(logged.Properties["OrganizationId"]));
        Assert.Equal(userId, Scalar(completion["UserId"]));
        Assert.Equal(organizationId, Scalar(completion["OrganizationId"]));
        Assert.Equal(userId, activity.GetTagItem("fixflow.user_id"));
        Assert.Equal(organizationId, activity.GetTagItem("fixflow.organization_id"));
    }

    [Fact]
    public async Task InvokeAsync_AnonymousUser_AddsNothing()
    {
        var (logged, completion, activity) = await InvokeAsync(new ClaimsPrincipal(new ClaimsIdentity()));

        Assert.Empty(logged.Properties);
        Assert.Empty(completion);
        Assert.Empty(activity.Tags);
    }

    private static async Task<(LogEvent Logged, Dictionary<string, LogEventPropertyValue> Completion, Activity Activity)>
        InvokeAsync(ClaimsPrincipal user)
    {
        var sink = new TestLogSink();
        using var logger = new LoggerConfiguration().Enrich.FromLogContext().WriteTo.Sink(sink).CreateLogger();
        var diagnosticContext = new DiagnosticContext(logger);
        using var collector = diagnosticContext.BeginCollection();
        using var activity = new Activity("request").Start();

        var middleware = new RequestLogContextMiddleware(
            _ =>
            {
                logger.Information("Handling the request");
                return Task.CompletedTask;
            },
            diagnosticContext,
            Options.Create(new RequestLogContextOptions { OrganizationIdClaimType = OrganizationClaimType }));

        await middleware.InvokeAsync(new DefaultHttpContext { User = user });

        Assert.True(collector.TryComplete(out var properties, out _));
        return (Assert.Single(sink.Events), properties!.ToDictionary(p => p.Name, p => p.Value), activity);
    }

    private static object? Scalar(LogEventPropertyValue value) => Assert.IsType<ScalarValue>(value).Value;
}
