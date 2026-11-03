using System.Net;
using System.Net.Http.Json;
using FixFlow.API.IntegrationTests.Fixtures;
using FixFlow.API.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FixFlow.API.IntegrationTests.Identity;

/// <summary>
/// Signup, login, the two password reset steps and the password change share the <see cref="RateLimitPolicies.Auth"/>
/// policy, with one budget per endpoint and client address. Each test client has a /64 of its own
/// (<see cref="WebApplicationFactoryExtensions.CreateApiClient"/>), so these tests use up only their own budgets.
/// </summary>
public class AuthRateLimitTests(FixFlowApiFactory api)
{
    private const string WrongPassword = "not the password";
    private const string NewPassword = "a brand new passphrase";

    /// <summary>Parses as no reset code, so the reset answers 422 without looking up an account.</summary>
    private const string NoResetCode = "not-a-reset-code";

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private AuthRateLimitOptions Limits => api.Services.GetRequiredService<IOptions<AuthRateLimitOptions>>().Value;

    [Fact]
    public async Task LogIn_OverTheLimit_Returns429WithRetryAfter()
    {
        var account = await api.SignUpElsewhereAsync();
        using var client = api.CreateApiClient();
        await UseUpLogInBudgetAsync(client);
        using var otherClient = api.CreateApiClient();

        // The limiter answers before the handler: even the right password gets no session
        using var rejected = await client.LogInAsync(account.Email, account.Password);
        using var otherAddress = await otherClient.LogInAsync(TestData.Email(), WrongPassword);

        var problem = await rejected.ReadProblemAsync(HttpStatusCode.TooManyRequests);
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("detail").GetString()));
        var retryAfter = rejected.Headers.RetryAfter?.Delta;
        Assert.NotNull(retryAfter);
        Assert.InRange(retryAfter.Value, TimeSpan.FromSeconds(1), Limits.Window);
        Assert.Null(AuthCookie.Find(rejected));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, otherAddress.StatusCode);
    }

    [Fact]
    public async Task LogIn_OverTheLimit_LeavesSignUpItsOwnBudget()
    {
        using var client = api.CreateApiClient();
        await UseUpLogInBudgetAsync(client);

        using var response = await client.PostAsJsonAsync(IdentityClientExtensions.SignUpPath, TestData.SignUp(), _ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task SignUp_OverTheLimit_Returns429()
    {
        using var client = api.CreateApiClient();
        var request = TestData.SignUp();

        // Invalid bodies count too
        for (var attempt = 1; attempt <= Limits.PermitLimit; attempt++)
        {
            using var invalid = await client.PostAsJsonAsync(IdentityClientExtensions.SignUpPath, new { }, _ct);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }

        using var rejected = await client.PostAsJsonAsync(IdentityClientExtensions.SignUpPath, request, _ct);

        await rejected.ReadProblemAsync(HttpStatusCode.TooManyRequests);
        Assert.NotNull(rejected.Headers.RetryAfter);
        Assert.Null(await api.FindUserAsync(request.Email));
    }

    [Fact]
    public async Task LogIn_OtherAddressInTheSame64_SharesTheBudget()
    {
        var address = WebApplicationFactoryExtensions.NextClientAddress();
        using var client = api.CreateApiClient(address);
        await UseUpLogInBudgetAsync(client);
        using var neighbour = api.CreateApiClient(InSame64(address, host: 2));

        using var response = await neighbour.LogInAsync(TestData.Email(), WrongPassword);

        await response.ReadProblemAsync(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task ForgotPassword_OverTheLimit_Returns429AndSendsNothing()
    {
        var account = await api.SignUpElsewhereAsync();
        using var client = api.CreateApiClient();
        await UseUpBudgetAsync(client, PasswordEndpoint.ForgotPassword);

        using var rejected = await client.ForgotPasswordAsync(account.Email);

        await AssertTooManyAsync(rejected);
        Assert.Empty(api.Emails.To(account.Email));
    }

    [Fact]
    public async Task ResetPassword_OverTheLimit_Returns429AndLeavesTheLinkWorking()
    {
        var account = await api.SignUpElsewhereAsync();
        var token = await api.RequestResetTokenAsync(account.Email);
        using var client = api.CreateApiClient();
        await UseUpBudgetAsync(client, PasswordEndpoint.ResetPassword);

        using var rejected = await client.ResetPasswordAsync(token, NewPassword);

        await AssertTooManyAsync(rejected);
        // The limiter answered before the handler, so the link is still unused
        using var otherClient = api.CreateApiClient();
        using var reset = await otherClient.ResetPasswordAsync(token, NewPassword);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_OverTheLimit_Returns429AndKeepsThePassword()
    {
        using var client = api.CreateApiClient();
        var account = await client.SignUpAsync();
        await UseUpBudgetAsync(client, PasswordEndpoint.ChangePassword);

        using var rejected = await client.ChangePasswordAsync(account.Password, NewPassword);

        await AssertTooManyAsync(rejected);
        using var fresh = api.CreateApiClient();
        using var logIn = await fresh.LogInAsync(account.Email, account.Password);
        Assert.Equal(HttpStatusCode.NoContent, logIn.StatusCode);
    }

    [Theory]
    [InlineData(PasswordEndpoint.ForgotPassword)]
    [InlineData(PasswordEndpoint.ResetPassword)]
    [InlineData(PasswordEndpoint.ChangePassword)]
    public async Task PasswordEndpoint_OverTheLimit_LeavesTheOthersTheirBudgets(PasswordEndpoint usedUp)
    {
        // Signed in, so the password change answers past its authorization
        using var client = api.CreateApiClient();
        await client.SignUpAsync();
        await UseUpBudgetAsync(client, usedUp);

        using var rejected = await HarmlessCallAsync(client, usedUp);

        await rejected.ReadProblemAsync(HttpStatusCode.TooManyRequests);
        foreach (var other in Enum.GetValues<PasswordEndpoint>().Where(endpoint => endpoint != usedUp))
        {
            using var response = await HarmlessCallAsync(client, other);
            Assert.Equal(WithinBudget(other), response.StatusCode);
        }

        using var logIn = await client.LogInAsync(TestData.Email(), WrongPassword);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, logIn.StatusCode);
    }

    public enum PasswordEndpoint
    {
        ForgotPassword,
        ResetPassword,
        ChangePassword,
    }

    /// <summary>Uses up the client's budget on <paramref name="endpoint"/> with calls that change no account.</summary>
    private async Task UseUpBudgetAsync(HttpClient client, PasswordEndpoint endpoint)
    {
        for (var attempt = 1; attempt <= Limits.PermitLimit; attempt++)
        {
            using var response = await HarmlessCallAsync(client, endpoint);
            Assert.Equal(WithinBudget(endpoint), response.StatusCode);
        }
    }

    /// <summary>
    /// A call to <paramref name="endpoint"/> that changes no account. A password change needs a signed-in
    /// <paramref name="client"/>.
    /// </summary>
    private static Task<HttpResponseMessage> HarmlessCallAsync(HttpClient client, PasswordEndpoint endpoint) =>
        endpoint switch
        {
            // An unknown email answers like a known one, and sends nothing
            PasswordEndpoint.ForgotPassword => client.ForgotPasswordAsync(TestData.Email()),
            PasswordEndpoint.ResetPassword => client.ResetPasswordAsync(NoResetCode, NewPassword),
            // Fails validation before the current password is checked: wrong ones would lock the account after five
            PasswordEndpoint.ChangePassword => client.ChangePasswordAsync(WrongPassword, "too short"),
            _ => throw new ArgumentOutOfRangeException(nameof(endpoint), endpoint, null),
        };

    /// <summary>The answer to <see cref="HarmlessCallAsync"/> while the budget lasts.</summary>
    private static HttpStatusCode WithinBudget(PasswordEndpoint endpoint) => endpoint switch
    {
        PasswordEndpoint.ForgotPassword => HttpStatusCode.Accepted,
        PasswordEndpoint.ResetPassword => HttpStatusCode.UnprocessableEntity,
        PasswordEndpoint.ChangePassword => HttpStatusCode.BadRequest,
        _ => throw new ArgumentOutOfRangeException(nameof(endpoint), endpoint, null),
    };

    /// <summary>A 429 Problem Details whose <c>Retry-After</c> is within the window.</summary>
    private async Task AssertTooManyAsync(HttpResponseMessage response)
    {
        await response.ReadProblemAsync(HttpStatusCode.TooManyRequests);
        var retryAfter = response.Headers.RetryAfter?.Delta;
        Assert.NotNull(retryAfter);
        Assert.InRange(retryAfter.Value, TimeSpan.FromSeconds(1), Limits.Window);
    }

    /// <summary>
    /// Uses up the client's login budget with unknown emails, which answer like a wrong password without locking any
    /// account.
    /// </summary>
    private async Task UseUpLogInBudgetAsync(HttpClient client)
    {
        for (var attempt = 1; attempt <= Limits.PermitLimit; attempt++)
        {
            using var failed = await client.LogInAsync(TestData.Email(), WrongPassword);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, failed.StatusCode);
        }
    }

    /// <summary><paramref name="address"/> with its low 64 bits replaced by <paramref name="host"/>.</summary>
    private static IPAddress InSame64(IPAddress address, byte host)
    {
        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        bytes[15] = host;
        return new IPAddress(bytes);
    }
}
