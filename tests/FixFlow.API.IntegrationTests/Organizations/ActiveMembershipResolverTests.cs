using System.Net;
using System.Net.Http.Json;
using FixFlow.API.IntegrationTests.Fixtures;
using FixFlow.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace FixFlow.API.IntegrationTests.Organizations;

/// <summary>
/// The membership resolution behind the organization context, through <see cref="OrganizationContextTestEndpoints"/>:
/// what it finds, that it caches hits and misses, and that every membership event invalidates the user's entries.
/// </summary>
public class ActiveMembershipResolverTests(FixFlowApiFactory api)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Find_Member_ReturnsTheOrganizationAndItsRoles()
    {
        using var client = api.CreateApiClient();
        var signedUp = await client.SignUpAsync();

        var membership = await FindAsync(client, signedUp.Slug);

        Assert.NotNull(membership);
        Assert.Equal(signedUp.OrganizationId, membership.OrganizationId);
        Assert.Equal(["Admin"], membership.Roles);
    }

    [Fact]
    public async Task Find_AnotherUsersOrganization_ReturnsNull()
    {
        using var client = api.CreateApiClient();
        await client.SignUpAsync();
        var other = await api.SignUpElsewhereAsync();

        Assert.Null(await FindAsync(client, other.Slug));
    }

    [Fact]
    public async Task Find_UnknownSlug_ReturnsNull()
    {
        using var client = api.CreateApiClient();
        await client.SignUpAsync();

        Assert.Null(await FindAsync(client, TestData.OrganizationSlug()));
    }

    [Fact]
    public async Task Find_OwnSlugInUpperCase_ReturnsNull()
    {
        using var client = api.CreateApiClient();
        var signedUp = await client.SignUpAsync();

        // Slugs are taken as written (plan P21)
        Assert.Null(await FindAsync(client, signedUp.Slug.ToUpperInvariant()));
    }

    [Theory]
    [InlineData("api")] // reserved
    [InlineData("ab")] // too short
    [InlineData("org--x")] // double hyphen
    public async Task Find_InvalidSlug_ReturnsNull(string slug)
    {
        using var client = api.CreateApiClient();
        await client.SignUpAsync();

        Assert.Null(await FindAsync(client, slug));
    }

    [Fact]
    public async Task Find_SuspendedMember_ReturnsNull()
    {
        using var client = api.CreateApiClient();
        await client.SignUpAsync();
        var suspended = await client.CreateOrganizationAsync(TestData.Name("Org"), TestData.OrganizationSlug());
        var userId = (await client.GetMeAsync()).Id;
        await api.ChangeMembershipAsync(suspended.Id, userId, membership => membership.Suspend());

        Assert.Null(await FindAsync(client, suspended.Slug));
    }

    [Fact]
    public async Task Find_ChangedWithoutEvents_StaysCached()
    {
        using var client = api.CreateApiClient();
        var signedUp = await client.SignUpAsync();
        var userId = (await client.GetMeAsync()).Id;
        Assert.NotNull(await FindAsync(client, signedUp.Slug));

        await SetStatusWithoutEventsAsync(signedUp.OrganizationId, userId, MembershipStatus.Suspended);

        // The database has the suspension (/me isn't cached), the cache doesn't: nothing invalidated it
        Assert.Empty((await client.GetMeAsync()).Memberships);
        Assert.NotNull(await FindAsync(client, signedUp.Slug));
    }

    [Fact]
    public async Task Find_MissChangedWithoutEvents_StaysAMiss()
    {
        using var client = api.CreateApiClient();
        await client.SignUpAsync();
        var second = await client.CreateOrganizationAsync(TestData.Name("Org"), TestData.OrganizationSlug());
        var userId = (await client.GetMeAsync()).Id;
        await api.ChangeMembershipAsync(second.Id, userId, membership => membership.Suspend());
        Assert.Null(await FindAsync(client, second.Slug));

        await SetStatusWithoutEventsAsync(second.Id, userId, MembershipStatus.Active);

        Assert.Contains(second.Slug, (await client.GetMeAsync()).Memberships.Select(m => m.Slug));
        Assert.Null(await FindAsync(client, second.Slug));
    }

    [Fact]
    public async Task Find_AfterSuspension_ReturnsNull()
    {
        using var client = api.CreateApiClient();
        var signedUp = await client.SignUpAsync();
        var userId = (await client.GetMeAsync()).Id;
        Assert.NotNull(await FindAsync(client, signedUp.Slug));

        await api.ChangeMembershipAsync(signedUp.OrganizationId, userId, membership => membership.Suspend());

        Assert.Null(await FindAsync(client, signedUp.Slug));
    }

    [Fact]
    public async Task Find_AfterReactivation_ReturnsTheMembership()
    {
        using var client = api.CreateApiClient();
        var signedUp = await client.SignUpAsync();
        var userId = (await client.GetMeAsync()).Id;
        await api.ChangeMembershipAsync(signedUp.OrganizationId, userId, membership => membership.Suspend());
        Assert.Null(await FindAsync(client, signedUp.Slug));

        await api.ChangeMembershipAsync(signedUp.OrganizationId, userId, membership => membership.Reactivate());

        Assert.Equal(signedUp.OrganizationId, (await FoundAsync(client, signedUp.Slug)).OrganizationId);
    }

    [Fact]
    public async Task Find_AfterRoleChange_ReturnsTheNewRoles()
    {
        using var client = api.CreateApiClient();
        var signedUp = await client.SignUpAsync();
        var userId = (await client.GetMeAsync()).Id;
        Assert.Equal(["Admin"], (await FoundAsync(client, signedUp.Slug)).Roles);

        await api.ChangeMembershipAsync(
            signedUp.OrganizationId, userId, membership => membership.ChangeRoles([Role.Admin, Role.Technician]));

        Assert.Equal(["Technician", "Admin"], (await FoundAsync(client, signedUp.Slug)).Roles);
    }

    [Fact]
    public async Task Find_AfterJoining_ReturnsTheNewMembershipWithItsOwnRoles()
    {
        using var founder = api.CreateApiClient();
        var shared = await founder.SignUpAsync();
        using var joiner = api.CreateApiClient();
        await joiner.SignUpAsync();
        var joinerId = (await joiner.GetMeAsync()).Id;
        Assert.Null(await FindAsync(joiner, shared.Slug));
        Assert.NotNull(await FindAsync(founder, shared.Slug));

        // No invites before T3.2: the user joins the organization directly
        await api.JoinAsync(shared.OrganizationId, joinerId, Role.Manager, Role.Technician);

        // Each user has their own entries: the founder's are untouched by the joiner's
        Assert.Equal(["Technician", "Manager"], (await FoundAsync(joiner, shared.Slug)).Roles);
        Assert.Equal(["Admin"], (await FoundAsync(founder, shared.Slug)).Roles);
    }

    /// <summary>The caller's active membership in <paramref name="slug"/>'s organization, or null.</summary>
    private async Task<ResolvedMembership?> FindAsync(HttpClient client, string slug)
    {
        using var response = await client.GetAsync(
            $"{OrganizationContextTestEndpoints.Path}/memberships/{Uri.EscapeDataString(slug)}", _ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var lookup = await response.Content.ReadFromJsonAsync<Lookup>(_ct);
        return lookup?.Membership;
    }

    /// <summary>The caller's active membership in <paramref name="slug"/>'s organization, which must exist.</summary>
    private async Task<ResolvedMembership> FoundAsync(HttpClient client, string slug) =>
        await FindAsync(client, slug) ?? throw new InvalidOperationException($"No active membership in {slug}.");

    /// <summary>Changes the status in SQL only, without the aggregate: no event, so nothing invalidates the cache.</summary>
    private async Task SetStatusWithoutEventsAsync(Guid organizationId, Guid userId, MembershipStatus status) =>
        await api.InOrganizationAsync(organizationId, db => db.OrganizationMemberships
            .Where(m => m.UserId == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(m => m.Status, status), _ct));

    private sealed record Lookup(ResolvedMembership? Membership);

    /// <param name="Roles">As the JSON names them.</param>
    private sealed record ResolvedMembership(Guid OrganizationId, string[] Roles);
}
