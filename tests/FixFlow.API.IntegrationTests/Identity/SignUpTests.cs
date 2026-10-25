using System.Net;
using System.Net.Http.Json;
using FixFlow.API.IntegrationTests.Fixtures;
using FixFlow.Domain.Organizations;

namespace FixFlow.API.IntegrationTests.Identity;

public class SignUpTests(FixFlowApiFactory api)
{
    private const string EmailTaken = "An account with this email already exists.";

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task SignUp_Valid_Returns201CookieAndAdminMembership()
    {
        using var client = api.CreateApiClient();
        var request = TestData.SignUp();

        using var response = await client.PostAsJsonAsync(IdentityClientExtensions.SignUpPath, request, _ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        // Organizations have no GET endpoint yet
        Assert.Null(response.Headers.Location);
        Assert.NotNull(AuthCookie.Find(response));
        var organization = await response.Content.ReadFromJsonAsync<CreatedOrganization>(_ct);
        Assert.Equal(7, organization!.Id.Version);
        Assert.Equal(request.OrganizationName, organization.Name);
        Assert.Equal(request.Slug, organization.Slug);

        var me = await client.GetMeAsync();
        Assert.Equal(request.Email, me.Email);
        var membership = Assert.Single(me.Memberships);
        Assert.Equal(organization.Id, membership.OrganizationId);
        Assert.Equal(request.Slug, membership.Slug);
        Assert.Equal(["Admin"], membership.Roles);
    }

    [Fact]
    public async Task SignUp_EmailTaken_Returns422OnEmail()
    {
        using var first = api.CreateApiClient();
        var signedUp = await first.SignUpAsync();

        foreach (var email in new[] { signedUp.Email, signedUp.Email.ToUpperInvariant() })
        {
            using var client = api.CreateApiClient();
            using var response = await client.PostAsJsonAsync(
                IdentityClientExtensions.SignUpPath, TestData.SignUp() with { Email = email }, _ct);

            var problem = await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity);
            Assert.Equal([EmailTaken], problem.FieldErrors()["email"]);
            Assert.Null(AuthCookie.Find(response));
        }
    }

    [Fact]
    public async Task SignUp_SlugTaken_Returns422OnSlug_AndRollsBackTheUser()
    {
        using var first = api.CreateApiClient();
        var taken = await first.SignUpAsync();
        using var client = api.CreateApiClient();
        var request = TestData.SignUp() with { Slug = taken.Slug };

        using var response = await client.PostAsJsonAsync(IdentityClientExtensions.SignUpPath, request, _ct);

        var problem = await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity);
        Assert.Equal(["This slug is already taken."], problem.FieldErrors()["slug"]);
        Assert.Null(await api.FindUserAsync(request.Email));
        // The email is free again: the account was rolled back with the organization
        await client.SignUpAsync(request with { Slug = TestData.OrganizationSlug() });
    }

    [Fact]
    public async Task SignUp_NineCharacterPassword_Returns400OnPassword()
    {
        using var client = api.CreateApiClient();

        using var response = await client.PostAsJsonAsync(
            IdentityClientExtensions.SignUpPath, TestData.SignUp() with { Password = "ninechars" }, _ct);

        var problem = await response.ReadProblemAsync(HttpStatusCode.BadRequest);
        Assert.Equal(["password"], problem.FieldErrors().Keys);
    }

    [Fact]
    public async Task SignUp_ReservedSlug_Returns400OnSlug()
    {
        using var client = api.CreateApiClient();
        Assert.False(OrganizationSlug.IsValid("login", out var expected));

        using var response = await client.PostAsJsonAsync(
            IdentityClientExtensions.SignUpPath, TestData.SignUp() with { Slug = "login" }, _ct);

        var problem = await response.ReadProblemAsync(HttpStatusCode.BadRequest);
        Assert.Equal([expected], problem.FieldErrors()["slug"]);
    }

    [Fact]
    public async Task SignUp_WhileSignedIn_Returns422()
    {
        using var client = api.CreateApiClient();
        var signedUp = await client.SignUpAsync();
        var second = TestData.SignUp();

        using var response = await client.PostAsJsonAsync(IdentityClientExtensions.SignUpPath, second, _ct);

        var problem = await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity);
        Assert.False(problem.TryGetProperty("errors", out _));
        Assert.Null(AuthCookie.Find(response));
        Assert.Null(await api.FindUserAsync(second.Email));
        var me = await client.GetMeAsync();
        Assert.Equal(signedUp.Email, me.Email);
        Assert.Equal([signedUp.Slug], me.Memberships.Select(m => m.Slug));
    }

    [Fact]
    public async Task SignUp_PaddedEmail_StoredTrimmed()
    {
        using var client = api.CreateApiClient();
        var email = TestData.Email();

        await client.SignUpAsync(TestData.SignUp() with { Email = $"  {email} " });

        Assert.Equal(email, (await client.GetMeAsync()).Email);
        using var another = api.CreateApiClient();
        using var logIn = await another.LogInAsync(email, TestData.Password);
        Assert.Equal(HttpStatusCode.NoContent, logIn.StatusCode);
    }

    [Fact]
    public async Task SignUp_Failures_LogNoEmail()
    {
        using var first = api.CreateApiClient();
        var signedUp = await first.SignUpAsync();
        SignUpRequest[] failures =
        [
            TestData.SignUp() with { Email = signedUp.Email },
            TestData.SignUp() with { Slug = signedUp.Slug },
            TestData.SignUp() with { Password = "ninechars" },
        ];

        foreach (var request in failures)
        {
            using var client = api.CreateApiClient();
            using var response = await client.PostAsJsonAsync(IdentityClientExtensions.SignUpPath, request, _ct);
            Assert.False(response.IsSuccessStatusCode);
        }

        Assert.All(failures, request => Assert.Empty(api.Logs.Mentioning(request.Email)));
        // The handlers' events reach the sink, by id
        var userId = (await api.FindUserAsync(signedUp.Email))!.Id;
        Assert.NotEmpty(api.Logs.Mentioning(userId.ToString()));
    }
}
