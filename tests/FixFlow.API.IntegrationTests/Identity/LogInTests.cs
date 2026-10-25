using System.Net;
using System.Text.Json;
using FixFlow.API.IntegrationTests.Fixtures;
using FixFlow.Infrastructure.Identity;

namespace FixFlow.API.IntegrationTests.Identity;

public class LogInTests(FixFlowApiFactory api)
{
    private const string WrongPassword = "not the password";

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task LogIn_RightPassword_Returns204AndSessionCookie()
    {
        var account = await SignUpElsewhereAsync();
        using var client = api.CreateApiClient();

        using var response = await client.LogInAsync(account.Email, account.Password);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var cookie = AuthCookie.Find(response);
        Assert.NotNull(cookie);
        // A session cookie: the browser drops it when it closes
        Assert.Null(cookie.Expires);
        Assert.Equal(account.Email, (await client.GetMeAsync()).Email);
    }

    [Fact]
    public async Task LogIn_RememberMe_SetsPersistentCookie()
    {
        var account = await SignUpElsewhereAsync();
        using var client = api.CreateApiClient();

        using var response = await client.LogInAsync(account.Email, account.Password, rememberMe: true);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var cookie = AuthCookie.Find(response);
        Assert.NotNull(cookie);
        Assert.True(cookie.Expires > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task LogIn_FiveWrongPasswords_LocksOut()
    {
        var account = await SignUpElsewhereAsync();
        using var client = api.CreateApiClient();
        using var first = await client.LogInAsync(account.Email, WrongPassword);
        var wrongPassword = await first.ReadProblemAsync(HttpStatusCode.UnprocessableEntity);

        for (var attempt = 2; attempt <= FixFlowIdentityExtensions.MaxFailedSignIns; attempt++)
        {
            using var failed = await client.LogInAsync(account.Email, WrongPassword);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, failed.StatusCode);
        }

        using var rightPassword = await client.LogInAsync(account.Email, account.Password);

        var lockedOut = await rightPassword.ReadProblemAsync(HttpStatusCode.UnprocessableEntity);
        AssertSameAnswer(wrongPassword, lockedOut);
        Assert.Null(AuthCookie.Find(rightPassword));
    }

    [Fact]
    public async Task LogIn_UnknownEmail_AnswersLikeWrongPassword()
    {
        var account = await SignUpElsewhereAsync();
        using var client = api.CreateApiClient();

        using var wrong = await client.LogInAsync(account.Email, WrongPassword);
        using var unknown = await client.LogInAsync(TestData.Email(), WrongPassword);

        var wrongPassword = await wrong.ReadProblemAsync(HttpStatusCode.UnprocessableEntity);
        var unknownEmail = await unknown.ReadProblemAsync(HttpStatusCode.UnprocessableEntity);
        AssertSameAnswer(wrongPassword, unknownEmail);
        Assert.False(unknownEmail.TryGetProperty("errors", out _));
        Assert.False(string.IsNullOrWhiteSpace(unknownEmail.GetProperty("detail").GetString()));
    }

    [Fact]
    public async Task LogIn_FormPost_IsRefusedBeforeTheEndpoint()
    {
        // Login CSRF: an HTML form on another site can post only form encodings, and the endpoint accepts JSON only
        // (A11). Routing refuses the content type with an endpoint of its own, which isn't AllowAnonymous, so the
        // fallback policy answers an anonymous caller with 401 before that endpoint's 415.
        var account = await SignUpElsewhereAsync();
        using var anonymous = api.CreateApiClient();
        using var signedIn = api.CreateApiClient();
        await signedIn.SignUpAsync();

        using var anonymousPost = await anonymous.PostAsync(IdentityClientExtensions.LogInPath, Form(account), _ct);
        using var signedInPost = await signedIn.PostAsync(IdentityClientExtensions.LogInPath, Form(account), _ct);

        await anonymousPost.ReadProblemAsync(HttpStatusCode.Unauthorized);
        await signedInPost.ReadProblemAsync(HttpStatusCode.UnsupportedMediaType);
        Assert.Null(AuthCookie.Find(anonymousPost));
        Assert.Null(AuthCookie.Find(signedInPost));

        static FormUrlEncodedContent Form(SignedUp account) =>
            new([new("email", account.Email), new("password", account.Password)]);
    }

    [Fact]
    public async Task LogIn_Failures_LogNoEmail()
    {
        var account = await SignUpElsewhereAsync();
        var unknownEmail = TestData.Email();
        using var client = api.CreateApiClient();

        using var wrong = await client.LogInAsync(account.Email, WrongPassword);
        using var unknown = await client.LogInAsync(unknownEmail, WrongPassword);
        using var right = await client.LogInAsync(account.Email, account.Password);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, right.StatusCode);
        Assert.Empty(api.Logs.Mentioning(account.Email));
        Assert.Empty(api.Logs.Mentioning(unknownEmail));
        // The handler's events reach the sink, by id
        var userId = (await api.FindUserAsync(account.Email))!.Id.ToString();
        var templates = api.Logs.Mentioning(userId).Select(logEvent => logEvent.MessageTemplate.Text).ToList();
        Assert.Contains(templates, text => text.Contains("wrong password", StringComparison.Ordinal));
        Assert.Contains(templates, text => text.Contains("signed in", StringComparison.Ordinal));
    }

    /// <summary>An account that another client created, so the test's own client starts signed out.</summary>
    private async Task<SignedUp> SignUpElsewhereAsync()
    {
        using var client = api.CreateApiClient();
        return await client.SignUpAsync();
    }

    /// <summary>Every member but the trace id, which each response has its own of.</summary>
    private static void AssertSameAnswer(JsonElement expected, JsonElement actual)
    {
        foreach (var member in new[] { "status", "title", "type", "detail" })
        {
            Assert.Equal(expected.GetProperty(member).ToString(), actual.GetProperty(member).ToString());
        }
    }
}
