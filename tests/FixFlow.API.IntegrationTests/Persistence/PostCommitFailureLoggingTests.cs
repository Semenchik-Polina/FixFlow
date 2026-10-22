using System.Net;
using System.Net.Sockets;
using FixFlow.API.IntegrationTests.Fixtures;
using FixFlow.API.IntegrationTests.Persistence.TestModel;
using FixFlow.Application.Errors;
using FixFlow.Application.Events;
using FixFlow.Infrastructure.Logging;
using FixFlow.Infrastructure.Persistence.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Serilog;
using Serilog.Events;

namespace FixFlow.API.IntegrationTests.Persistence;

/// <summary>
/// How a failed post-commit handler's exception is logged (ADR 0007): types, safe fields and stack traces, no
/// messages. No database: each test dispatches one event to a handler that throws what the test gives it.
/// </summary>
public class PostCommitFailureLoggingTests(FixFlowApiFactory api)
{
    private const string Address = "jane.doe@example.test";

    [Fact]
    public async Task ExceptionChain_LogsTypesSafeFieldsAndStackTraces_WithoutMessages()
    {
        var socket = Thrown(new SocketException((int)SocketError.ConnectionRefused));

        var logged = await DispatchAsync(() => new InvalidOperationException($"Sending to {Address} failed", socket));

        var text = logged.Exception!.ToString();
        Assert.StartsWith("System.InvalidOperationException: [message removed]", text, StringComparison.Ordinal);
        Assert.Contains(
            " ---> System.Net.Sockets.SocketException [SocketErrorCode=ConnectionRefused]: [message removed]",
            text,
            StringComparison.Ordinal);
        Assert.Contains("--- End of inner exception stack trace ---", text, StringComparison.Ordinal);
        // Both stack traces: where the inner exception was thrown, and the handler
        Assert.Contains(nameof(Thrown), text, StringComparison.Ordinal);
        Assert.Contains(nameof(ThrowingSideEffectHandler), text, StringComparison.Ordinal);
        Assert.DoesNotContain(Address, text, StringComparison.Ordinal);
        Assert.DoesNotContain(socket.Message, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AggregateException_ListsEveryInnerException()
    {
        var logged = await DispatchAsync(() => new AggregateException(
            Thrown(new TimeoutException($"No answer for {Address}")),
            Thrown(new ArgumentException($"Bad address {Address}", "recipient"))));

        var text = logged.Exception!.ToString();
        // Its own message repeats the inner ones
        Assert.StartsWith("System.AggregateException: [message removed]", text, StringComparison.Ordinal);
        Assert.Contains(" ---> System.TimeoutException: [message removed]", text, StringComparison.Ordinal);
        Assert.Contains(
            " ---> (Inner Exception #1) System.ArgumentException [ParamName=recipient]: [message removed]",
            text,
            StringComparison.Ordinal);
        Assert.DoesNotContain(Address, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LogSafeMessage_IsKept_InnerMessagesAreNot()
    {
        var logged = await DispatchAsync(() => new SafeMessageException(
            "SMTP 550 MailboxUnavailable", Thrown(new InvalidOperationException($"<{Address}> rejected"))));

        var text = logged.Exception!.ToString();
        Assert.StartsWith(
            $"{typeof(SafeMessageException).FullName}: SMTP 550 MailboxUnavailable", text, StringComparison.Ordinal);
        Assert.Contains(" ---> System.InvalidOperationException: [message removed]", text, StringComparison.Ordinal);
        Assert.DoesNotContain(Address, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SafeFields_OfWellKnownExceptions_AreKept()
    {
        var http = await DispatchAsync(() => new HttpRequestException(
            HttpRequestError.ConnectionError, $"Calling {Address}", null, HttpStatusCode.BadGateway));
        var postgres = await DispatchAsync(() => new PostgresException(
            "duplicate key value violates unique constraint",
            "ERROR",
            "ERROR",
            PostgresErrorCodes.UniqueViolation,
            detail: $"Key (email)=({Address}) already exists.",
            constraintName: "ix_users_email"));

        Assert.StartsWith(
            "System.Net.Http.HttpRequestException [StatusCode=BadGateway, HttpRequestError=ConnectionError]: "
            + "[message removed]",
            http.Exception!.ToString(),
            StringComparison.Ordinal);
        Assert.StartsWith(
            "Npgsql.PostgresException [SqlState=23505, ConstraintName=ix_users_email]: [message removed]",
            postgres.Exception!.ToString(),
            StringComparison.Ordinal);
        Assert.DoesNotContain(Address, http.Exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Address, postgres.Exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task KeepMessages_LogsTheExceptionAsThrown()
    {
        var thrown = new InvalidOperationException($"Sending to {Address} failed");

        var logged = await DispatchAsync(() => thrown, keepMessages: true);

        Assert.Same(thrown, logged.Exception);
    }

    [Fact]
    public void Api_OutsideDevelopment_RemovesMessages()
    {
        var options = api.Services.GetRequiredService<IOptions<ExceptionLoggingOptions>>().Value;

        Assert.False(options.KeepMessages);
    }

    /// <summary>Dispatches one <see cref="WidgetSideEffectThrew"/> and returns the failure it logged.</summary>
    private static async Task<LogEvent> DispatchAsync(Func<Exception> create, bool keepMessages = false)
    {
        var logs = new TestLogSink();
        await using var services = new ServiceCollection()
            .AddLogging(logging => logging.AddSerilog(
                new LoggerConfiguration().WriteTo.Sink(logs).CreateLogger(), dispose: true))
            .Configure<ExceptionLoggingOptions>(options => options.KeepMessages = keepMessages)
            .AddScoped<DomainEventDispatcher>()
            .AddTransient<IPostCommitEventHandler<WidgetSideEffectThrew>, ThrowingSideEffectHandler>()
            .BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();

        await scope.ServiceProvider
            .GetRequiredService<DomainEventDispatcher>()
            .DispatchPostCommitAsync(new WidgetSideEffectThrew(create));

        return Assert.Single(logs.Events);
    }

    /// <summary>Throws and catches it, so it has a stack trace like a real inner exception.</summary>
    private static TException Thrown<TException>(TException exception)
        where TException : Exception
    {
        try
        {
            throw exception;
        }
        catch (TException caught)
        {
            return caught;
        }
    }

    private sealed class SafeMessageException(string message, Exception innerException)
        : Exception(message, innerException), ILogSafeMessage;
}
