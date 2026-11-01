using System.Net;
using FixFlow.API.Auth;
using FixFlow.API.IntegrationTests.Fixtures;
using FixFlow.Domain.Organizations;

namespace FixFlow.API.IntegrationTests.Auth;

/// <summary>
/// The role <see cref="Policies"/> through the app's pipeline (<see cref="PolicyTestEndpoints"/>): who passes each one,
/// with the roles of the organization the request names, and what the others get.
/// </summary>
public class RolePolicyTests(FixFlowApiFactory api)
{
    /// <summary>
    /// Who may pass each policy (§7, §8, A9), written out here rather than read from <see cref="Policies.Roles"/>, so the
    /// test is the specification the catalog is checked against.
    /// </summary>
    private static readonly Dictionary<string, Role[]> _allowed = new()
    {
        [Policies.CanManageOrganization] = [Role.Admin],
        [Policies.CanManageMembers] = [Role.Manager, Role.Admin],
        [Policies.CanManageProperties] = [Role.Manager, Role.Admin],
        [Policies.CanSubmitRequests] = [Role.Tenant],
        [Policies.CanTriageRequests] = [Role.Manager, Role.Admin],
        [Policies.CanWorkOnJobs] = [Role.Technician, Role.Manager, Role.Admin],
    };

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public static TheoryData<string> EveryPolicy => [.. _allowed.Keys];

    [Fact]
    public void Catalog_EveryPolicy_HasItsExpectedRoles()
    {
        // A new policy needs its row here
        Assert.Equal(_allowed.Keys.Order(StringComparer.Ordinal), Policies.Roles.Keys.Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(EveryPolicy))]
    public async Task Policy_EachRole_PassesOnlyWithAnAllowedRole(string policy)
    {
        using var organization = await api.OrganizationWithEveryRoleAsync();

        var passed = new List<Role>();
        foreach (var role in Enum.GetValues<Role>())
        {
            using var response = await organization.As(role).GetAsync(PolicyTestEndpoints.PathOf(policy), _ct);
            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                passed.Add(role);
            }
            else
            {
                // A member without the role: Problem Details, no redirect to an access-denied page
                await response.ReadProblemAsync(HttpStatusCode.Forbidden);
                Assert.Null(response.Headers.Location);
            }
        }

        Assert.Equal(_allowed[policy].Order(), passed.Order());
    }

    [Fact]
    public async Task Policy_ManagerInATenantInB_FollowsTheRolesOfTheOrganizationNamed()
    {
        var a = await api.SignUpElsewhereAsync();
        var b = await api.SignUpElsewhereAsync();
        var member = await api.MemberOfBothAsync(a.OrganizationId, [Role.Manager], b.OrganizationId, [Role.Tenant]);
        using var client = await api.SignInAsync(member);

        // A manager endpoint with B's header: B's roles apply, and the Manager role in A grants nothing there
        using (var managerInB = await GetAsync(client, Policies.CanManageProperties, b.Slug))
        {
            await managerInB.ReadProblemAsync(HttpStatusCode.Forbidden);
        }

        using var managerInA = await GetAsync(client, Policies.CanManageProperties, a.Slug);
        using var tenantInB = await GetAsync(client, Policies.CanSubmitRequests, b.Slug);
        using var tenantInA = await GetAsync(client, Policies.CanSubmitRequests, a.Slug);
        Assert.Equal(HttpStatusCode.NoContent, managerInA.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, tenantInB.StatusCode);
        await tenantInA.ReadProblemAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Policy_MemberWithSeveralRoles_PassesThePoliciesOfEach()
    {
        var organization = await api.SignUpElsewhereAsync();
        var member = await api.AddMemberAsync(organization.OrganizationId, Role.Technician, Role.Tenant);
        using var client = (await api.SignInAsync(member)).WithOrganization(organization.Slug);

        using var jobs = await client.GetAsync(PolicyTestEndpoints.PathOf(Policies.CanWorkOnJobs), _ct);
        using var requests = await client.GetAsync(PolicyTestEndpoints.PathOf(Policies.CanSubmitRequests), _ct);
        using var properties = await client.GetAsync(PolicyTestEndpoints.PathOf(Policies.CanManageProperties), _ct);

        Assert.Equal(HttpStatusCode.NoContent, jobs.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, requests.StatusCode);
        await properties.ReadProblemAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Policy_Anonymous_Returns401()
    {
        var organization = await api.SignUpElsewhereAsync();
        using var client = api.CreateApiClient().WithOrganization(organization.Slug);

        using var response = await client.GetAsync(PolicyTestEndpoints.PathOf(Policies.CanWorkOnJobs), _ct);

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Policy_AnotherOrganizationsSlugWithoutTheRole_Returns404Not403()
    {
        var other = await api.SignUpElsewhereAsync();
        using var client = api.CreateApiClient();
        await client.SignUpAsync();

        // The organization context refuses a non-member before any policy runs, so a 403 can't tell that the slug exists
        using var response = await GetAsync(client, Policies.CanSubmitRequests, other.Slug);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Policy_NextRequestAfterARoleChange_UsesTheNewRoles()
    {
        var organization = await api.SignUpElsewhereAsync();
        var member = await api.AddMemberAsync(organization.OrganizationId, Role.Manager);
        using var client = (await api.SignInAsync(member)).WithOrganization(organization.Slug);
        var path = PolicyTestEndpoints.PathOf(Policies.CanManageProperties);
        // Caches the membership
        using (var asManager = await client.GetAsync(path, _ct))
        {
            Assert.Equal(HttpStatusCode.NoContent, asManager.StatusCode);
        }

        await api.ChangeMembershipAsync(
            organization.OrganizationId, member.UserId, membership => membership.ChangeRoles([Role.Tenant]));

        using var asTenant = await client.GetAsync(path, _ct);
        await asTenant.ReadProblemAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Policy_OnAnOrganizationLessEndpoint_Returns403EvenToTheAdmin()
    {
        using var client = api.CreateApiClient();
        var founder = await client.SignUpAsync();
        client.WithOrganization(founder.Slug);

        // The endpoint ignores the header, so the founder has no roles there: a role policy fails closed
        using var response = await client.GetAsync(
            new Uri(PolicyTestEndpoints.WithoutOrganizationPath, UriKind.Relative), _ct);

        await response.ReadProblemAsync(HttpStatusCode.Forbidden);
    }

    // The header on this request only: the client sends none of its own
    private async Task<HttpResponseMessage> GetAsync(HttpClient client, string policy, string slug)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, PolicyTestEndpoints.PathOf(policy)).WithOrganization(slug);
        return await client.SendAsync(request, _ct);
    }
}
