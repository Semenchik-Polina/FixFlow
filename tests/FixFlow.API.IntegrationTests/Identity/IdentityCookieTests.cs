using System.Net;
using System.Net.Http.Json;
using FixFlow.API.IntegrationTests.Fixtures;
using Microsoft.Net.Http.Headers;

namespace FixFlow.API.IntegrationTests.Identity;

public class IdentityCookieTests(FixFlowApiFactory api)
{
    private static readonly Uri _session = new($"{IdentityTestEndpoints.Path}/session", UriKind.Relative);

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task SignUp_SetsCookie_HttpOnlySecureLax()
    {
        using var client = api.CreateApiClient();

        using var response = await client.PostAsJsonAsync(IdentityClientExtensions.SignUpPath, TestData.SignUp(), _ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var cookie = AuthCookie.Find(response);
        Assert.NotNull(cookie);
        Assert.True(cookie.HttpOnly);
        Assert.True(cookie.Secure);
        Assert.Equal(SameSiteMode.Lax, cookie.SameSite);
    }

    [Fact]
    public async Task SignedIn_CookieRoundTrip_ResolvesCurrentUser()
    {
        using var client = api.CreateApiClient();
        var signedUp = await client.SignUpAsync();

        // Validating the cookie resolves the security stamp validator on every request, so a missing registration
        // fails here. The reset and change-password tests cover a changed stamp ending a session.
        var current = await client.GetFromJsonAsync<Session>(_session, _ct);

        Assert.Equal(await UserIdAsync(signedUp.Email), current!.UserId);
    }

    [Fact]
    public async Task SignedIn_Request_LeavesTheCookieAlone()
    {
        // The stamp is checked on every request (plan T1.4 D1), but a matching one re-issues nothing: a slow response
        // that started before a password change would otherwise put back the old stamp and end the session
        using var client = api.CreateApiClient();
        var signedUp = await client.SignUpAsync();

        using var response = await client.GetAsync(_session, _ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(AuthCookie.Find(response));
        Assert.Equal(signedUp.Email, (await client.GetMeAsync()).Email);
    }

    [Fact]
    public async Task SignedIn_Principal_CarriesNoEmail()
    {
        using var client = api.CreateApiClient();
        var signedUp = await client.SignUpAsync();

        var current = await client.GetFromJsonAsync<Session>(_session, _ct);

        // S26: anything that logs the principal logs ids only. The name claim is the id too (plan P22).
        Assert.DoesNotContain(
            current!.ClaimValues, value => value.Contains(signedUp.Email, StringComparison.OrdinalIgnoreCase));
        Assert.Contains((await UserIdAsync(signedUp.Email)).ToString(), current.ClaimValues);
    }

    private async Task<Guid> UserIdAsync(string email) =>
        (await api.FindUserAsync(email) ?? throw new InvalidOperationException("No account with this email.")).Id;
}
