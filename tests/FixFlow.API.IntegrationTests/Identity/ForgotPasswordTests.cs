using System.Net;
using FixFlow.API.IntegrationTests.Fixtures;
using FixFlow.Application.Identity.ForgotPassword;
using FixFlow.Application.Identity.ResetPassword;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Serilog.Events;

namespace FixFlow.API.IntegrationTests.Identity;

public class ForgotPasswordTests(FixFlowApiFactory api)
{
    [Fact]
    public async Task ForgotPassword_UnknownEmail_Returns202AndSendsNothing()
    {
        var email = TestData.Email();
        using var client = api.CreateApiClient();

        using var response = await client.ForgotPasswordAsync(email);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Empty(api.Emails.To(email));
    }

    [Fact]
    public async Task ForgotPassword_KnownEmail_Returns202AndSendsTheLink()
    {
        var account = await api.SignUpElsewhereAsync();
        using var client = api.CreateApiClient();

        using var response = await client.ForgotPasswordAsync(account.Email);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var message = Assert.Single(api.Emails.To(account.Email));
        Assert.Equal(PasswordResetEmail.Subject, message.Subject);
        // From checks that the HTML body links to the same address as the text body
        var link = ResetLink.From(message);
        Assert.StartsWith(
            $"{FixFlowApiFactory.SpaPublicUrl.AbsoluteUri}reset-password#token=", link.AbsoluteUri, StringComparison.Ordinal);
        Assert.NotEmpty(ResetLink.Token(link));
    }

    [Fact]
    public async Task ForgotPassword_SignedInAsAnotherUser_SendsOnlyToTheNamedAccount()
    {
        // The account analogue of a cross-tenant test: the email picks the account, never the caller's session
        var account = await api.SignUpElsewhereAsync();
        using var caller = api.CreateApiClient();
        var callerAccount = await caller.SignUpAsync();

        using var response = await caller.ForgotPasswordAsync(account.Email);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Empty(api.Emails.To(callerAccount.Email));
        // The link's code names the account the email belongs to
        var token = ResetLink.Token(ResetLink.From(Assert.Single(api.Emails.To(account.Email))));
        Assert.True(PasswordResetCode.TryParse(token, out var userId, out _));
        Assert.Equal((await api.FindUserAsync(account.Email))!.Id, userId);
        Assert.Equal(callerAccount.Email, (await caller.GetMeAsync()).Email);
    }

    [Fact]
    public async Task ForgotPassword_DifferentCaseOrPadding_FindsTheAccount()
    {
        var account = await api.SignUpElsewhereAsync();
        using var client = api.CreateApiClient();

        using var response = await client.ForgotPasswordAsync($"  {account.Email.ToUpperInvariant()} ");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        // To the address as the account stores it, not as typed
        Assert.Equal(account.Email, Assert.Single(api.Emails.To(account.Email)).To);
    }

    [Fact]
    public async Task ForgotPassword_MalformedEmail_Returns400()
    {
        using var client = api.CreateApiClient();

        using var response = await client.ForgotPasswordAsync("not-an-email");

        var problem = await response.ReadProblemAsync(HttpStatusCode.BadRequest);
        Assert.Equal(["email"], problem.FieldErrors().Keys);
    }

    [Fact]
    public async Task ForgotPassword_LogsNoEmailOrToken()
    {
        var account = await api.SignUpElsewhereAsync();
        var unknownEmail = TestData.Email();
        using var client = api.CreateApiClient();

        using var known = await client.ForgotPasswordAsync(account.Email);
        using var unknown = await client.ForgotPasswordAsync(unknownEmail);

        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);
        var token = ResetLink.LatestToken(api.Emails, account.Email);
        Assert.Empty(api.Logs.Mentioning(account.Email));
        Assert.Empty(api.Logs.Mentioning(unknownEmail));
        Assert.Empty(api.Logs.Mentioning(token));
        // The handler's events reach the sink, by id
        var userId = (await api.FindUserAsync(account.Email))!.Id.ToString();
        Assert.Contains(
            api.Logs.Mentioning(userId),
            logEvent => logEvent.MessageTemplate.Text.Contains("reset email sent", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ForgotPassword_LinkCantBeBuilt_Returns202AndLogsTheFailure()
    {
        // Spa:PublicUrl is checked on first use (plan P31), which only a known email reaches
        await using var app = api.WithWebHostBuilder(builder => builder.UseSetting("Spa:PublicUrl", "not a url"));
        var account = await api.SignUpElsewhereAsync();
        using var client = app.CreateApiClient();

        using var response = await client.ForgotPasswordAsync(account.Email);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Empty(api.Emails.To(account.Email));
        var userId = (await api.FindUserAsync(account.Email))!.Id.ToString();
        var failure = Assert.Single(api.Logs.Mentioning(userId), logEvent => logEvent.Level == LogEventLevel.Error);
        Assert.Contains(nameof(OptionsValidationException), failure.Exception!.ToString(), StringComparison.Ordinal);
    }
}
