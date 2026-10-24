using System.Net;
using System.Net.Http.Json;
using FixFlow.API.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace FixFlow.API.IntegrationTests.Identity;

public class IdentityCookieTests(FixFlowApiFactory api)
{
    // Cookie authentication names its cookie after the scheme
    private static readonly string _cookieName =
        CookieAuthenticationDefaults.CookiePrefix + IdentityConstants.ApplicationScheme;

    private static readonly Uri _signIn = new($"{IdentityTestEndpoints.Path}/sign-in", UriKind.Relative);

    private static readonly Uri _session = new($"{IdentityTestEndpoints.Path}/session", UriKind.Relative);

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task SignIn_SetsCookie_HttpOnlySecureLax()
    {
        using var client = api.CreateApiClient();

        using var response = await client.PostAsync(_signIn, content: null, _ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = Assert.Single(
            response.Headers.GetValues("Set-Cookie"),
            header => header.StartsWith($"{_cookieName}=", StringComparison.Ordinal));
        var attributes = cookie.Split(';').Skip(1).Select(a => a.Trim().ToLowerInvariant()).ToList();
        Assert.Contains("httponly", attributes);
        Assert.Contains("secure", attributes);
        Assert.Contains("samesite=lax", attributes);
    }

    [Fact]
    public async Task SignedIn_CookieRoundTrip_ResolvesCurrentUser()
    {
        using var client = api.CreateApiClient();
        using var signIn = await client.PostAsync(_signIn, content: null, _ct);
        var signedIn = await signIn.Content.ReadFromJsonAsync<SignedIn>(_ct);

        // Validating the cookie resolves the security stamp validator on every request, so a missing registration
        // fails here. Comparing the stamps waits for ValidationInterval; T1.4 covers it.
        var current = await client.GetFromJsonAsync<Session>(_session, _ct);

        Assert.Equal(signedIn!.UserId, current!.UserId);
    }

    [Fact]
    public async Task SignedIn_Principal_CarriesNoEmail()
    {
        using var client = api.CreateApiClient();
        using var signIn = await client.PostAsync(_signIn, content: null, _ct);
        var signedIn = await signIn.Content.ReadFromJsonAsync<SignedIn>(_ct);

        var current = await client.GetFromJsonAsync<Session>(_session, _ct);

        // S26: anything that logs the principal logs ids only. The name claim is the id too (plan P22).
        Assert.DoesNotContain(
            current!.ClaimValues, value => value.Contains(signedIn!.Email, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(signedIn!.UserId.ToString(), current.ClaimValues);
    }
}
