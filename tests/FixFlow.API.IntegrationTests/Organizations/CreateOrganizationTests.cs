using System.Net;
using System.Net.Http.Json;
using FixFlow.API.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace FixFlow.API.IntegrationTests.Organizations;

public class CreateOrganizationTests(FixFlowApiFactory api)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateOrganization_SignedIn_Returns201AndAdminMembership()
    {
        using var client = api.CreateApiClient();
        var signedUp = await client.SignUpAsync();
        var name = TestData.Name("Org");
        var slug = TestData.OrganizationSlug();

        using var response = await client.PostAsJsonAsync(
            IdentityClientExtensions.OrganizationsPath, new { name, slug }, _ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        // Organizations have no GET endpoint yet
        Assert.Null(response.Headers.Location);
        var created = await response.Content.ReadFromJsonAsync<CreatedOrganization>(_ct);
        Assert.Equal(7, created!.Id.Version);
        Assert.Equal(name, created.Name);
        Assert.Equal(slug, created.Slug);

        var me = await client.GetMeAsync();
        var membership = Assert.Single(me.Memberships, m => m.OrganizationId == created.Id);
        Assert.Equal(["Admin"], membership.Roles);
        Assert.Contains(me.Memberships, m => m.Slug == signedUp.Slug);
        // The audit stamps credit the caller: unlike signup, there is one
        var createdBy = await api.InOrganizationAsync(created.Id, db => db.Organizations
            .Where(o => o.Id == created.Id)
            .Select(o => o.CreatedBy)
            .SingleAsync(_ct));
        Assert.Equal(me.Id, createdBy);
    }

    [Fact]
    public async Task CreateOrganization_SlugTaken_Returns422OnSlug()
    {
        using var client = api.CreateApiClient();
        var signedUp = await client.SignUpAsync();

        using var response = await client.PostAsJsonAsync(
            IdentityClientExtensions.OrganizationsPath, new { name = TestData.Name("Org"), slug = signedUp.Slug }, _ct);

        var problem = await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity);
        Assert.Equal(["This slug is already taken."], problem.FieldErrors()["slug"]);
        Assert.Single((await client.GetMeAsync()).Memberships);
    }

    [Fact]
    public async Task CreateOrganization_Anonymous_Returns401()
    {
        using var client = api.CreateApiClient();
        var slug = TestData.OrganizationSlug();

        using var response = await client.PostAsJsonAsync(
            IdentityClientExtensions.OrganizationsPath, new { name = TestData.Name("Org"), slug }, _ct);

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
        // Nothing was created: the slug is still free
        await client.SignUpAsync(TestData.SignUp() with { Slug = slug });
    }

    [Fact]
    public async Task CreateOrganization_LeavesOtherOrganizationsUntouched()
    {
        using var client = api.CreateApiClient();
        var signedUp = await client.SignUpAsync();
        // Ignored before T1.5; afterwards the middleware must leave this endpoint alone, so the header changes nothing
        client.WithOrganization(signedUp.Slug);

        var created = await client.CreateOrganizationAsync(TestData.Name("Org"), TestData.OrganizationSlug());

        var membershipsInFirst = await api.InOrganizationAsync(
            signedUp.OrganizationId, db => db.OrganizationMemberships.CountAsync(_ct));
        var membershipsInNew = await api.InOrganizationAsync(
            created.Id, db => db.OrganizationMemberships.CountAsync(_ct));
        Assert.Equal(1, membershipsInFirst);
        Assert.Equal(1, membershipsInNew);
    }
}
