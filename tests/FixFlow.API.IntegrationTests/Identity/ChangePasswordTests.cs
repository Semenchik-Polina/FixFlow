using System.Net;
using FixFlow.API.IntegrationTests.Fixtures;
using FixFlow.Infrastructure.Identity;

namespace FixFlow.API.IntegrationTests.Identity;

public class ChangePasswordTests(FixFlowApiFactory api)
{
    private const string NewPassword = "a brand new passphrase";
    private const string WrongPassword = "not the password";

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task ChangePassword_RightCurrent_Returns204_KeepsThisSession_EndsOthers()
    {
        var account = await api.SignUpElsewhereAsync();
        using var client = await LogInAsync(account, rememberMe: true);
        using var other = await LogInAsync(account, rememberMe: false);

        using var response = await client.ChangePasswordAsync(account.Password, NewPassword);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        // A new cookie with the new stamp, still persistent ("Keep me signed in")
        var cookie = AuthCookie.Find(response);
        Assert.NotNull(cookie);
        Assert.True(cookie.Expires > DateTimeOffset.UtcNow);
        Assert.Equal(account.Email, (await client.GetMeAsync()).Email);
        using var otherMe = await other.GetAsync(IdentityClientExtensions.MePath, _ct);
        await otherMe.ReadProblemAsync(HttpStatusCode.Unauthorized);
        using var fresh = api.CreateApiClient();
        using var oldPassword = await fresh.LogInAsync(account.Email, account.Password);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, oldPassword.StatusCode);
        using var newPassword = await fresh.LogInAsync(account.Email, NewPassword);
        Assert.Equal(HttpStatusCode.NoContent, newPassword.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_SessionCookie_StaysASessionCookie()
    {
        using var client = api.CreateApiClient();
        var account = await client.SignUpAsync();

        using var response = await client.ChangePasswordAsync(account.Password, NewPassword);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var cookie = AuthCookie.Find(response);
        Assert.NotNull(cookie);
        Assert.Null(cookie.Expires);
        Assert.Equal(account.Email, (await client.GetMeAsync()).Email);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrent_Returns422OnCurrentPassword()
    {
        using var client = api.CreateApiClient();
        var account = await client.SignUpAsync();

        using var response = await client.ChangePasswordAsync(WrongPassword, NewPassword);

        var problem = await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity);
        Assert.Equal(["currentPassword"], problem.FieldErrors().Keys);
        // Still signed in, with the old password
        Assert.Equal(account.Email, (await client.GetMeAsync()).Email);
        using var fresh = api.CreateApiClient();
        using var logIn = await fresh.LogInAsync(account.Email, account.Password);
        Assert.Equal(HttpStatusCode.NoContent, logIn.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_FiveWrong_LocksOut()
    {
        // A stolen session can't be used to guess the password (S3)
        using var client = api.CreateApiClient();
        var account = await client.SignUpAsync();
        for (var attempt = 1; attempt <= FixFlowIdentityExtensions.MaxFailedSignIns; attempt++)
        {
            using var failed = await client.ChangePasswordAsync(WrongPassword, NewPassword);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, failed.StatusCode);
        }

        using var rightCurrent = await client.ChangePasswordAsync(account.Password, NewPassword);
        using var fresh = api.CreateApiClient();
        using var logIn = await fresh.LogInAsync(account.Email, account.Password);

        var lockedOut = await rightCurrent.ReadProblemAsync(HttpStatusCode.UnprocessableEntity);
        Assert.Equal(["currentPassword"], lockedOut.FieldErrors().Keys);
        await logIn.ReadProblemAsync(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task ChangePassword_VoidsOutstandingResetLinks()
    {
        using var client = api.CreateApiClient();
        var account = await client.SignUpAsync();
        var token = await api.RequestResetTokenAsync(account.Email);
        using var change = await client.ChangePasswordAsync(account.Password, NewPassword);
        using var anonymous = api.CreateApiClient();

        using var reset = await anonymous.ResetPasswordAsync(token, "yet another passphrase");

        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
        await reset.ReadProblemAsync(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task ChangePassword_OtherUsersUntouched()
    {
        // The account analogue of a cross-tenant test: the request names no user, so only the caller's password changes
        using var caller = api.CreateApiClient();
        var callerAccount = await caller.SignUpAsync();
        using var other = api.CreateApiClient();
        var otherAccount = await other.SignUpAsync();

        using var response = await caller.ChangePasswordAsync(callerAccount.Password, NewPassword);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        // The other account keeps its session and its password
        Assert.Equal(otherAccount.Email, (await other.GetMeAsync()).Email);
        using var fresh = api.CreateApiClient();
        using var logIn = await fresh.LogInAsync(otherAccount.Email, otherAccount.Password);
        Assert.Equal(HttpStatusCode.NoContent, logIn.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_Anonymous_Returns401()
    {
        using var client = api.CreateApiClient();

        using var response = await client.ChangePasswordAsync(WrongPassword, NewPassword);

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangePassword_ShortNew_Returns400()
    {
        using var client = api.CreateApiClient();
        var account = await client.SignUpAsync();

        using var response = await client.ChangePasswordAsync(account.Password, "too short");

        var problem = await response.ReadProblemAsync(HttpStatusCode.BadRequest);
        Assert.Equal(["newPassword"], problem.FieldErrors().Keys);
    }

    /// <summary>A new client, signed in to <paramref name="account"/>.</summary>
    private async Task<HttpClient> LogInAsync(SignedUp account, bool rememberMe)
    {
        var client = api.CreateApiClient();
        using var response = await client.LogInAsync(account.Email, account.Password, rememberMe);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        return client;
    }
}
