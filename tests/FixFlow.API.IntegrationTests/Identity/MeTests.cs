using System.Net;
using FixFlow.API.IntegrationTests.Fixtures;
using FixFlow.Domain.Organizations;

namespace FixFlow.API.IntegrationTests.Identity;

public class MeTests(FixFlowApiFactory api)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Me_ListsOnlyActiveMemberships()
    {
        using var client = api.CreateApiClient();
        var signedUp = await client.SignUpAsync();
        var suspended = await client.CreateOrganizationAsync(TestData.Name("Org"), TestData.OrganizationSlug());
        var userId = (await client.GetMeAsync()).Id;

        await api.ChangeMembershipAsync(suspended.Id, userId, membership => membership.Suspend());

        var me = await client.GetMeAsync();
        Assert.Equal([signedUp.Slug], me.Memberships.Select(m => m.Slug));
    }

    [Fact]
    public async Task Me_OtherMembersOfTheOrganization_AreNotListed()
    {
        // Named so that the expected order is known: memberships come ordered by the organization's name
        using var first = api.CreateApiClient();
        var shared = await first.SignUpAsync(TestData.SignUp() with { OrganizationName = TestData.Name("Alpha") });
        using var second = api.CreateApiClient();
        var own = await second.SignUpAsync(TestData.SignUp() with { OrganizationName = TestData.Name("Bravo") });
        var secondUserId = (await second.GetMeAsync()).Id;
        // No invites before T3.2: the second user joins the first user's organization directly
        await api.JoinAsync(shared.OrganizationId, secondUserId, Role.Admin, Role.Technician);

        var firstMe = await first.GetMeAsync();
        var secondMe = await second.GetMeAsync();

        var firstMembership = Assert.Single(firstMe.Memberships);
        Assert.Equal(shared.Slug, firstMembership.Slug);
        Assert.Equal(["Admin"], firstMembership.Roles);
        Assert.Equal([shared.Slug, own.Slug], secondMe.Memberships.Select(m => m.Slug));
        // Each membership has its own roles, in the order of Role rather than alphabetical
        Assert.Equal(["Technician", "Admin"], secondMe.Memberships[0].Roles);
        Assert.Equal(["Admin"], secondMe.Memberships[1].Roles);
    }

    [Fact]
    public async Task Me_Anonymous_Returns401()
    {
        using var client = api.CreateApiClient();

        using var response = await client.GetAsync(IdentityClientExtensions.MePath, _ct);

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
    }
}
