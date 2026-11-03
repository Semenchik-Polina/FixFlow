using System.Net;
using FixFlow.API.IntegrationTests.Fixtures;
using Serilog.Events;

namespace FixFlow.API.IntegrationTests.Identity;

/// <summary>
/// Forgot-password over real SMTP (<see cref="FixFlowApiFactory.WithRealEmail"/>): a failed send still answers 202, and
/// its log names the failure without the address (S26, ADR 0007).
/// </summary>
public class ForgotPasswordEmailFailureTests(FixFlowApiFactory api)
{
    private const string NewPassword = "a brand new passphrase";

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task ForgotPassword_RecipientRejected_Returns202AndLogsTheSmtpStatus()
    {
        await using var app = api.WithRealEmail(api.RejectingSmtp);
        var account = await app.SignUpElsewhereAsync();
        using var client = app.CreateApiClient();

        using var response = await client.ForgotPasswordAsync(account.Email);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var failure = await SingleFailureAsync(app, account.Email);
        var exception = failure.Exception!.ToString();
        Assert.Contains("550", exception, StringComparison.Ordinal);
        Assert.Contains("RecipientNotAccepted", exception, StringComparison.Ordinal);
        AssertNoAddressLogged(app, account.Email);
    }

    [Fact]
    public async Task ForgotPassword_SmtpDown_Returns202AndLogsConnectFailure()
    {
        await using var app = api.WithRealEmail(SmtpEndpoint.Closed());
        var account = await app.SignUpElsewhereAsync();
        using var client = app.CreateApiClient();

        using var response = await client.ForgotPasswordAsync(account.Email);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var failure = await SingleFailureAsync(app, account.Email);
        Assert.Contains("SocketErrorCode=ConnectionRefused", failure.Exception!.ToString(), StringComparison.Ordinal);
        AssertNoAddressLogged(app, account.Email);
    }

    [Fact]
    public async Task ForgotPassword_RealSender_LinkFromMailpitResetsThePassword()
    {
        await using var app = api.WithRealEmail(api.MailpitSmtp);
        var account = await app.SignUpElsewhereAsync();
        using var client = app.CreateApiClient();

        using var forgot = await client.ForgotPasswordAsync(account.Email);
        var message = await api.Mailpit.SingleToAsync(account.Email, _ct);
        using var reset = await client.ResetPasswordAsync(ResetLink.Token(ResetLink.In(message.Text)), NewPassword);

        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        using var logIn = await client.LogInAsync(account.Email, NewPassword);
        Assert.Equal(HttpStatusCode.NoContent, logIn.StatusCode);
    }

    /// <summary>The one Error event about the account's user id: the failed send.</summary>
    private static async Task<LogEvent> SingleFailureAsync(RealEmailApiFactory app, string email)
    {
        var userId = (await app.FindUserAsync(email))!.Id.ToString();
        var failure = Assert.Single(app.Logs.Mentioning(userId), logEvent => logEvent.Level == LogEventLevel.Error);
        Assert.NotNull(failure.Exception);
        return failure;
    }

    private static void AssertNoAddressLogged(RealEmailApiFactory app, string email)
    {
        Assert.Empty(app.Logs.Mentioning(email));
        Assert.Empty(app.Logs.Mentioning(email[..email.IndexOf('@', StringComparison.Ordinal)]));
    }
}
