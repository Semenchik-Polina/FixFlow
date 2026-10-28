using System.Net;
using FixFlow.API.IntegrationTests.Fixtures;

namespace FixFlow.API.IntegrationTests.Identity;

public class ChangeNameTests(FixFlowApiFactory api)
{
    [Fact]
    public async Task ChangeName_Valid_Returns204_ShowsTrimmedInMe()
    {
        using var client = api.CreateApiClient();
        await client.SignUpAsync();

        using var response = await client.ChangeNameAsync("  Ada ", " Lovelace  ");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var me = await client.GetMeAsync();
        Assert.Equal("Ada", me.FirstName);
        Assert.Equal("Lovelace", me.LastName);
    }

    [Fact]
    public async Task ChangeName_KeepsOtherSessions()
    {
        // The security stamp stays: only a password reset or change ends sessions
        using var client = api.CreateApiClient();
        var account = await client.SignUpAsync();
        using var other = api.CreateApiClient();
        using (var logIn = await other.LogInAsync(account.Email, account.Password))
        {
            Assert.Equal(HttpStatusCode.NoContent, logIn.StatusCode);
        }

        using var response = await client.ChangeNameAsync("Ada", "Lovelace");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("Ada", (await other.GetMeAsync()).FirstName);
    }

    [Fact]
    public async Task ChangeName_OtherUsersUntouched()
    {
        // The account analogue of a cross-tenant test: the request names no user, so only the caller's row changes
        using var caller = api.CreateApiClient();
        await caller.SignUpAsync();
        using var other = api.CreateApiClient();
        await other.SignUpAsync();

        using var response = await caller.ChangeNameAsync("Ada", "Lovelace");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("Ada", (await caller.GetMeAsync()).FirstName);
        var otherMe = await other.GetMeAsync();
        Assert.Equal(("Test", "User"), (otherMe.FirstName, otherMe.LastName));
    }

    [Fact]
    public async Task ChangeName_Blank_Returns400()
    {
        using var client = api.CreateApiClient();
        await client.SignUpAsync();

        using var response = await client.ChangeNameAsync("", "   ");

        var problem = await response.ReadProblemAsync(HttpStatusCode.BadRequest);
        Assert.Equal(["firstName", "lastName"], problem.FieldErrors().Keys.Order());
    }

    [Fact]
    public async Task ChangeName_ControlCharacters_Returns400()
    {
        using var client = api.CreateApiClient();
        await client.SignUpAsync();

        using var response = await client.ChangeNameAsync("Ada\nBob", "Love\0lace");

        var problem = await response.ReadProblemAsync(HttpStatusCode.BadRequest);
        Assert.Equal(["firstName", "lastName"], problem.FieldErrors().Keys.Order());
        Assert.Equal("Test", (await client.GetMeAsync()).FirstName);
    }

    [Fact]
    public async Task ChangeName_Anonymous_Returns401()
    {
        using var client = api.CreateApiClient();

        using var response = await client.ChangeNameAsync("Ada", "Lovelace");

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
    }
}
