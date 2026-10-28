using System.Net;
using FixFlow.API.IntegrationTests.Fixtures;
using FixFlow.Application.Identity.ResetPassword;
using FixFlow.Infrastructure.Identity;
using NodaTime;

namespace FixFlow.API.IntegrationTests.Identity;

public class ResetPasswordTests(FixFlowApiFactory api)
{
    private const string NewPassword = "a brand new passphrase";
    private const string WrongPassword = "not the password";

    /// <summary>S4: a reset link works for an hour.</summary>
    private static readonly Duration _lifespan = Duration.FromHours(1);

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task ResetPassword_ValidToken_Returns204_NewPasswordLogsIn_OldDoesNot()
    {
        var account = await api.SignUpElsewhereAsync();
        var token = await api.RequestResetTokenAsync(account.Email);
        using var client = api.CreateApiClient();

        using var response = await client.ResetPasswordAsync(token, NewPassword);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        // Signs no one in: the SPA goes on to its login page
        Assert.Null(AuthCookie.Find(response));
        using var oldPassword = await client.LogInAsync(account.Email, account.Password);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, oldPassword.StatusCode);
        using var newPassword = await client.LogInAsync(account.Email, NewPassword);
        Assert.Equal(HttpStatusCode.NoContent, newPassword.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_TokenUsedTwice_Returns422()
    {
        var account = await api.SignUpElsewhereAsync();
        var token = await api.RequestResetTokenAsync(account.Email);
        using var client = api.CreateApiClient();
        using var first = await client.ResetPasswordAsync(token, NewPassword);

        using var second = await client.ResetPasswordAsync(token, "yet another passphrase");

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        await AssertInvalidLinkAsync(second);
        using var logIn = await client.LogInAsync(account.Email, NewPassword);
        Assert.Equal(HttpStatusCode.NoContent, logIn.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_OlderToken_AfterAReset_Returns422()
    {
        var account = await api.SignUpElsewhereAsync();
        var older = await api.RequestResetTokenAsync(account.Email);
        var newer = await api.RequestResetTokenAsync(account.Email);
        using var client = api.CreateApiClient();
        using var reset = await client.ResetPasswordAsync(newer, NewPassword);

        using var response = await client.ResetPasswordAsync(older, "yet another passphrase");

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        await AssertInvalidLinkAsync(response);
    }

    [Fact]
    public async Task ResetPassword_JustBeforeOneHour_Works()
    {
        await using var app = api.WithOwnClock(FixFlowApiFactory.SharedNow);
        var account = await app.SignUpElsewhereAsync();
        var token = await app.RequestResetTokenAsync(account.Email);
        app.Clock.Advance(_lifespan - Duration.Epsilon);
        using var client = app.CreateApiClient();

        using var response = await client.ResetPasswordAsync(token, NewPassword);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_AtOneHour_Returns422()
    {
        await using var app = api.WithOwnClock(FixFlowApiFactory.SharedNow);
        var account = await app.SignUpElsewhereAsync();
        var token = await app.RequestResetTokenAsync(account.Email);
        app.Clock.Advance(_lifespan);
        using var client = app.CreateApiClient();

        using var response = await client.ResetPasswordAsync(token, NewPassword);

        await AssertInvalidLinkAsync(response);
    }

    [Fact]
    public async Task ResetPassword_EndsOtherSessions()
    {
        using var signedUp = api.CreateApiClient();
        var account = await signedUp.SignUpAsync();
        using var loggedIn = api.CreateApiClient();
        using (var logIn = await loggedIn.LogInAsync(account.Email, account.Password, rememberMe: true))
        {
            Assert.Equal(HttpStatusCode.NoContent, logIn.StatusCode);
        }

        var token = await api.RequestResetTokenAsync(account.Email);
        using var client = api.CreateApiClient();

        using var response = await client.ResetPasswordAsync(token, NewPassword);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        // On their very next request, with no interval to wait out (plan T1.4 D1)
        using var signedUpMe = await signedUp.GetAsync(IdentityClientExtensions.MePath, _ct);
        await signedUpMe.ReadProblemAsync(HttpStatusCode.Unauthorized);
        using var loggedInMe = await loggedIn.GetAsync(IdentityClientExtensions.MePath, _ct);
        await loggedInMe.ReadProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ResetPassword_LockedOutAccount_CanLogInAfterReset()
    {
        var account = await api.SignUpElsewhereAsync();
        using var client = api.CreateApiClient();
        for (var attempt = 1; attempt <= FixFlowIdentityExtensions.MaxFailedSignIns; attempt++)
        {
            using var failed = await client.LogInAsync(account.Email, WrongPassword);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, failed.StatusCode);
        }

        var token = await api.RequestResetTokenAsync(account.Email);

        using var response = await client.ResetPasswordAsync(token, NewPassword);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var logIn = await client.LogInAsync(account.Email, NewPassword);
        Assert.Equal(HttpStatusCode.NoContent, logIn.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_TokenForAnotherUser_Returns422()
    {
        // The account analogue of a cross-tenant test: A's token, with B's id in the code
        var victim = await api.SignUpElsewhereAsync();
        var attacker = await api.SignUpElsewhereAsync();
        var attackerCode = await api.RequestResetTokenAsync(attacker.Email);
        Assert.True(PasswordResetCode.TryParse(attackerCode, out _, out var attackerToken));
        var victimId = (await api.FindUserAsync(victim.Email))!.Id;
        using var client = api.CreateApiClient();

        using var response = await client.ResetPasswordAsync(PasswordResetCode.Format(victimId, attackerToken), NewPassword);

        await AssertInvalidLinkAsync(response);
        using var victimLogIn = await client.LogInAsync(victim.Email, victim.Password);
        Assert.Equal(HttpStatusCode.NoContent, victimLogIn.StatusCode);
        // The failed attempt used nothing up: the token still works for its own account
        using var attackerReset = await client.ResetPasswordAsync(attackerCode, NewPassword);
        Assert.Equal(HttpStatusCode.NoContent, attackerReset.StatusCode);
    }

    [Theory]
    [InlineData(Tampering.ChangedCharacter)]
    [InlineData(Tampering.CutShort)]
    [InlineData(Tampering.NotBase64Url)]
    [InlineData(Tampering.NoUserId)]
    public async Task ResetPassword_TamperedToken_Returns422(Tampering tampering)
    {
        var account = await api.SignUpElsewhereAsync();
        var code = await api.RequestResetTokenAsync(account.Email);
        using var client = api.CreateApiClient();

        using var response = await client.ResetPasswordAsync(Tamper(code, tampering), NewPassword);

        await AssertInvalidLinkAsync(response);
        using var logIn = await client.LogInAsync(account.Email, account.Password);
        Assert.Equal(HttpStatusCode.NoContent, logIn.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_ConcurrentResets_ExactlyOneSucceeds()
    {
        var account = await api.SignUpElsewhereAsync();
        var token = await api.RequestResetTokenAsync(account.Email);
        using var first = api.CreateApiClient();
        using var second = api.CreateApiClient();

        var responses = await Task.WhenAll(
            first.ResetPasswordAsync(token, "first new passphrase"),
            second.ResetPasswordAsync(token, "second new passphrase"));

        try
        {
            var succeeded = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.NoContent);
            await AssertInvalidLinkAsync(Assert.Single(responses, response => response != succeeded));
            var winner = succeeded == responses[0] ? "first new passphrase" : "second new passphrase";
            using var logIn = await first.LogInAsync(account.Email, winner);
            Assert.Equal(HttpStatusCode.NoContent, logIn.StatusCode);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task ResetPassword_ShortPassword_Returns400()
    {
        var account = await api.SignUpElsewhereAsync();
        var token = await api.RequestResetTokenAsync(account.Email);
        using var client = api.CreateApiClient();

        using var response = await client.ResetPasswordAsync(token, "too short");

        var problem = await response.ReadProblemAsync(HttpStatusCode.BadRequest);
        Assert.Equal(["newPassword"], problem.FieldErrors().Keys);
        // Validation answered before the token was checked, so it still works
        using var retry = await client.ResetPasswordAsync(token, NewPassword);
        Assert.Equal(HttpStatusCode.NoContent, retry.StatusCode);
    }

    public enum Tampering
    {
        /// <summary>One character of the token changed to another base64url character.</summary>
        ChangedCharacter,

        /// <summary>The token's last character removed.</summary>
        CutShort,

        /// <summary>A character base64url doesn't have.</summary>
        NotBase64Url,

        /// <summary>The token alone, without the user id in front.</summary>
        NoUserId,
    }

    private static string Tamper(string code, Tampering tampering)
    {
        Assert.True(PasswordResetCode.TryParse(code, out var userId, out var token));
        var middle = token.Length / 2;

        return tampering switch
        {
            Tampering.ChangedCharacter => PasswordResetCode.Format(
                userId, string.Concat(token.AsSpan(0, middle), token[middle] == 'A' ? "B" : "A", token.AsSpan(middle + 1))),
            Tampering.CutShort => PasswordResetCode.Format(userId, token[..^1]),
            Tampering.NotBase64Url => PasswordResetCode.Format(userId, token + "!"),
            Tampering.NoUserId => token,
            _ => throw new ArgumentOutOfRangeException(nameof(tampering), tampering, null),
        };
    }

    /// <summary>The one answer for every bad link: a 422 with a detail and no field.</summary>
    private static async Task AssertInvalidLinkAsync(HttpResponseMessage response)
    {
        var problem = await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity);
        Assert.False(problem.TryGetProperty("errors", out _));
        Assert.Equal(
            "This reset link is invalid or has expired. Ask for a new one.",
            problem.GetProperty("detail").GetString());
        Assert.Null(AuthCookie.Find(response));
    }
}
