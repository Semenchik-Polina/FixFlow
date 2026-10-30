using System.Net;
using System.Net.Http.Json;
using FixFlow.API.IntegrationTests.Fixtures;

namespace FixFlow.API.IntegrationTests.Organizations;

/// <summary>
/// The organization context middleware, through <see cref="OrganizationContextTestEndpoints"/>: what a request that names
/// the caller's organization gets, and how the others are refused.
/// </summary>
public class OrganizationContextTests(FixFlowApiFactory api)
{
    private static readonly Uri _contextPath = new($"{OrganizationContextTestEndpoints.Path}/context", UriKind.Relative);

    private static readonly Uri _membershipsPath =
        new($"{OrganizationContextTestEndpoints.Path}/memberships", UriKind.Relative);

    private static readonly Uri _trustedScopePath =
        new($"{OrganizationContextTestEndpoints.Path}/trusted-scope", UriKind.Relative);

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Context_OwnSlug_HasTheOrganizationAndTheCallersRoles()
    {
        using var client = api.CreateApiClient();
        var signedUp = await client.SignUpAsync();
        client.WithOrganization(signedUp.Slug);

        var context = await client.GetFromJsonAsync<ContextRead>(_contextPath, _ct);

        Assert.NotNull(context);
        Assert.Equal(signedUp.OrganizationId, context.OrganizationId);
        Assert.Equal(["Admin"], context.Roles);
    }

    [Fact]
    public async Task Context_NoHeader_Returns400()
    {
        using var client = api.CreateApiClient();
        await client.SignUpAsync();

        using var response = await client.GetAsync(_contextPath, _ct);

        var problem = await response.ReadProblemAsync(HttpStatusCode.BadRequest);
        Assert.Contains(
            HttpClientExtensions.OrganizationHeader, problem.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Context_AnotherOrganizationsSlug_Returns404()
    {
        using var client = api.CreateApiClient();
        await client.SignUpAsync();
        var other = await api.SignUpElsewhereAsync();
        client.WithOrganization(other.Slug);

        using var response = await client.GetAsync(_contextPath, _ct);

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Context_AnonymousWithUnknownSlug_Returns401()
    {
        using var client = api.CreateApiClient();
        client.WithOrganization(TestData.OrganizationSlug());

        using var response = await client.GetAsync(_contextPath, _ct);

        // Not a 404: an anonymous caller can't probe for slugs
        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Memberships_OwnSlug_SeesOnlyThatOrganization()
    {
        using var client = api.CreateApiClient();
        var signedUp = await client.SignUpAsync();
        var userId = (await client.GetMeAsync()).Id;
        client.WithOrganization(signedUp.Slug);

        var userIds = await client.GetFromJsonAsync<Guid[]>(_membershipsPath, _ct);

        // Other tests' organizations share the database: the filter keeps them out
        Assert.NotNull(userIds);
        Assert.Equal([userId], userIds);
    }

    [Fact]
    public async Task TrustedScope_InOrganizationRequest_Returns500()
    {
        using var client = api.CreateApiClient();
        var signedUp = await client.SignUpAsync();
        client.WithOrganization(signedUp.Slug);

        using var response = await client.PostAsync(_trustedScopePath, content: null, _ct);

        await response.ReadProblemAsync(HttpStatusCode.InternalServerError);
    }

    /// <param name="Roles">As the JSON names them.</param>
    private sealed record ContextRead(Guid? OrganizationId, string[] Roles);
}
