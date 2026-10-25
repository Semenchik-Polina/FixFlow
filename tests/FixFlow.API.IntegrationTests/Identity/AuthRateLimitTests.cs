using System.Net;
using System.Net.Http.Json;
using FixFlow.API.IntegrationTests.Fixtures;
using FixFlow.API.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FixFlow.API.IntegrationTests.Identity;

/// <summary>
/// Signup and login share the <see cref="RateLimitPolicies.Auth"/> policy, with one budget per endpoint and client
/// address. Each test client has a /64 of its own (<see cref="WebApplicationFactoryExtensions.CreateApiClient"/>), so
/// these tests use up only their own budgets.
/// </summary>
public class AuthRateLimitTests(FixFlowApiFactory api)
{
    private const string WrongPassword = "not the password";

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private AuthRateLimitOptions Limits => api.Services.GetRequiredService<IOptions<AuthRateLimitOptions>>().Value;

    [Fact]
    public async Task LogIn_OverTheLimit_Returns429WithRetryAfter()
    {
        var account = await SignUpElsewhereAsync();
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

    /// <summary>An account that another client created, so the test's own client starts signed out.</summary>
    private async Task<SignedUp> SignUpElsewhereAsync()
    {
        using var client = api.CreateApiClient();
        return await client.SignUpAsync();
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
