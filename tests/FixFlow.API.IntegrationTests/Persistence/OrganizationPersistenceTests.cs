using FixFlow.API.IntegrationTests.Fixtures;
using FixFlow.API.IntegrationTests.Persistence.TestModel;
using FixFlow.Application.Identity;
using FixFlow.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace FixFlow.API.IntegrationTests.Persistence;

public class OrganizationPersistenceTests(FixFlowApiFactory api)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Membership_SavedWithRoles_RoundTrips()
    {
        var (organization, user) = await CreateOrganizationAndUserAsync();
        var member = InOrganization(organization);

        var membership = await SaveMembershipAsync(member, user, Role.Manager, Role.Technician);

        await using var reader = api.Persistence.CreateScope(member);
        var saved = await reader.Db.OrganizationMemberships.SingleAsync(m => m.Id == membership.Id, _ct);
        Assert.Equal(organization.Id, saved.OrganizationId);
        Assert.Equal(user.Id, saved.UserId);
        Assert.Equal(MembershipStatus.Active, saved.Status);
        Assert.Equal([Role.Technician, Role.Manager], saved.Roles.Select(r => r.Role).Order());
    }

    [Fact]
    public async Task ChangeRoles_RemoveKeepAndAdd_Persists()
    {
        var (organization, user) = await CreateOrganizationAndUserAsync();
        var member = InOrganization(organization);
        var membership = await SaveMembershipAsync(member, user, Role.Manager, Role.Technician);

        await using (var scope = api.Persistence.CreateScope(member))
        {
            var saved = await scope.Db.OrganizationMemberships.SingleAsync(m => m.Id == membership.Id, _ct);
            saved.ChangeRoles([Role.Technician, Role.Admin]);
            await scope.Db.SaveChangesAsync(_ct);
        }

        await using var reader = api.Persistence.CreateScope(member);
        var roles = await reader.Db.OrganizationMemberships
            .Where(m => m.Id == membership.Id)
            .SelectMany(m => m.Roles.Select(r => r.Role))
            .ToListAsync(_ct);
        Assert.Equal([Role.Technician, Role.Admin], roles.Order());
    }

    [Fact]
    public async Task Membership_OfAnotherOrganization_IsInvisible()
    {
        var (organization, user) = await CreateOrganizationAndUserAsync();
        var membership = await SaveMembershipAsync(InOrganization(organization), user, Role.Admin);

        Assert.False(await MembershipExistsAsync(TestCurrentUser.InNewOrganization(), membership.Id));
        Assert.False(await MembershipExistsAsync(TestCurrentUser.WithoutOrganization(), membership.Id));
    }

    [Fact]
    public async Task MembershipRoles_QueriedOnTheirOwn_Throw()
    {
        await using var scope = api.Persistence.CreateScope(TestCurrentUser.InNewOrganization());

        // Owned by the membership: reachable only through it, so only under the organization filter
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.Db.Set<MembershipRole>().ToListAsync(_ct));
    }

    [Fact]
    public async Task Organization_SlugTaken_ViolatesUniqueIndex()
    {
        var slug = TestData.OrganizationSlug();
        await SaveOrganizationAsync(slug);

        await using var scope = api.Persistence.CreateScope(TestCurrentUser.WithoutOrganization());
        scope.Db.Organizations.Add(Organization.Create(TestData.Name("Org"), slug));

        await DatabaseAssertions.AssertUniqueViolationAsync(
            "ix_organizations_slug", () => scope.Db.SaveChangesAsync(_ct));
    }

    [Fact]
    public async Task Membership_SecondForSameUserAndOrganization_ViolatesUniqueIndex()
    {
        var (organization, user) = await CreateOrganizationAndUserAsync();
        var member = InOrganization(organization);
        await SaveMembershipAsync(member, user, Role.Tenant);

        await using var scope = api.Persistence.CreateScope(member);
        scope.Db.OrganizationMemberships.Add(
            OrganizationMembership.Create(user.Id, [Role.Technician], FixFlowApiFactory.SharedNow));

        await DatabaseAssertions.AssertUniqueViolationAsync(
            "ix_organization_memberships_user_id_organization_id", () => scope.Db.SaveChangesAsync(_ct));
    }

    /// <summary>A caller in <paramref name="organization"/>, as the organization context will open it (T1.5).</summary>
    private static TestCurrentUser InOrganization(Organization organization) =>
        new(Guid.CreateVersion7(), organization.Id);

    private async Task<(Organization Organization, User User)> CreateOrganizationAndUserAsync()
    {
        var organization = await SaveOrganizationAsync(TestData.OrganizationSlug());

        // Neither is organization-owned, so no organization context is needed
        await using var scope = api.Persistence.CreateScope(TestCurrentUser.WithoutOrganization());
        var user = new User(TestData.Email(), "Test", "User");
        scope.Db.Users.Add(user);
        await scope.Db.SaveChangesAsync(_ct);

        return (organization, user);
    }

    private async Task<Organization> SaveOrganizationAsync(string slug)
    {
        await using var scope = api.Persistence.CreateScope(TestCurrentUser.WithoutOrganization());
        var organization = Organization.Create(TestData.Name("Org"), slug);
        scope.Db.Organizations.Add(organization);
        await scope.Db.SaveChangesAsync(_ct);
        return organization;
    }

    private async Task<OrganizationMembership> SaveMembershipAsync(
        TestCurrentUser member, User user, params Role[] roles)
    {
        await using var scope = api.Persistence.CreateScope(member);
        var membership = OrganizationMembership.Create(user.Id, roles, FixFlowApiFactory.SharedNow);
        scope.Db.OrganizationMemberships.Add(membership);
        await scope.Db.SaveChangesAsync(_ct);
        return membership;
    }

    private async Task<bool> MembershipExistsAsync(TestCurrentUser caller, Guid membershipId)
    {
        await using var scope = api.Persistence.CreateScope(caller);
        return await scope.Db.OrganizationMemberships.AnyAsync(m => m.Id == membershipId, _ct);
    }
}
