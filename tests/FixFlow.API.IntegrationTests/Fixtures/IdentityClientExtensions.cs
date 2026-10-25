using System.Net;
using System.Net.Http.Json;
using FixFlow.API.Endpoints;

namespace FixFlow.API.IntegrationTests.Fixtures;

/// <summary>
/// The account endpoints as a client calls them: signing up and in leaves the client signed in, through its own
/// cookies (<see cref="WebApplicationFactoryExtensions.CreateApiClient"/>).
/// </summary>
public static class IdentityClientExtensions
{
    public static readonly Uri SignUpPath = new($"{EndpointModuleExtensions.ApiRoot}/auth/signup", UriKind.Relative);

    public static readonly Uri LogInPath = new($"{EndpointModuleExtensions.ApiRoot}/auth/login", UriKind.Relative);

    public static readonly Uri LogOutPath = new($"{EndpointModuleExtensions.ApiRoot}/auth/logout", UriKind.Relative);

    public static readonly Uri MePath = new($"{EndpointModuleExtensions.ApiRoot}/me", UriKind.Relative);

    public static readonly Uri OrganizationsPath = new($"{EndpointModuleExtensions.ApiRoot}/organizations", UriKind.Relative);

    /// <summary>Signs up with <paramref name="request"/> (<see cref="TestData.SignUp"/> by default) and stays signed in.</summary>
    public static async Task<SignedUp> SignUpAsync(this HttpClient client, SignUpRequest? request = null)
    {
        request ??= TestData.SignUp();

        using var response = await client.PostAsJsonAsync(SignUpPath, request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var organization = await ReadAsync<CreatedOrganization>(response);
        return new SignedUp(request.Email, request.Password, organization.Id, organization.Slug);
    }

    /// <summary>The response is the caller's to check and dispose.</summary>
    public static Task<HttpResponseMessage> LogInAsync(
        this HttpClient client, string email, string password, bool rememberMe = false) =>
        client.PostAsJsonAsync(LogInPath, new { email, password, rememberMe }, TestContext.Current.CancellationToken);

    /// <summary>Creates another organization as the signed-in caller.</summary>
    public static async Task<CreatedOrganization> CreateOrganizationAsync(this HttpClient client, string name, string slug)
    {
        using var response = await client.PostAsJsonAsync(
            OrganizationsPath, new { name, slug }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<CreatedOrganization>(response);
    }

    /// <summary><c>GET /me</c>, which must succeed.</summary>
    public static async Task<MeResponse> GetMeAsync(this HttpClient client)
    {
        using var response = await client.GetAsync(MePath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync<MeResponse>(response);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<T>(TestContext.Current.CancellationToken)
        ?? throw new InvalidOperationException("The response has no body.");
}

/// <summary>A new account, signed in on the client that created it, with its first organization.</summary>
public sealed record SignedUp(string Email, string Password, Guid OrganizationId, string Slug);

/// <summary>The 201 body of signup and <c>POST /organizations</c>.</summary>
public sealed record CreatedOrganization(Guid Id, string Name, string Slug);

/// <summary>The body of <c>GET /me</c>.</summary>
public sealed record MeResponse(Guid Id, string Email, string FirstName, string LastName, MyMembership[] Memberships);

/// <param name="Roles">As the JSON names them.</param>
public sealed record MyMembership(Guid OrganizationId, string OrganizationName, string Slug, string[] Roles);
