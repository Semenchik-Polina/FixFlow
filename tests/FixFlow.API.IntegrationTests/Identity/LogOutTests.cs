using System.Net;
using FixFlow.API.IntegrationTests.Fixtures;

namespace FixFlow.API.IntegrationTests.Identity;

public class LogOutTests(FixFlowApiFactory api)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task LogOut_SignedIn_ClearsCookie()
    {
        using var client = api.CreateApiClient();
        await client.SignUpAsync();

        using var response = await client.PostAsync(IdentityClientExtensions.LogOutPath, content: null, _ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        // An expired cookie, which the browser drops. A copy of the old one stays valid (cookie auth keeps no session).
        var cookie = AuthCookie.Find(response);
        Assert.NotNull(cookie);
        Assert.True(cookie.Expires < DateTimeOffset.UtcNow);
        using var me = await client.GetAsync(IdentityClientExtensions.MePath, _ct);
        await me.ReadProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LogOut_Anonymous_Returns401()
    {
        using var client = api.CreateApiClient();

        using var response = await client.PostAsync(IdentityClientExtensions.LogOutPath, content: null, _ct);

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
    }
}
