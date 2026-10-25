using FixFlow.API.IntegrationTests.Fixtures;
using FixFlow.API.IntegrationTests.Persistence.TestModel;
using FixFlow.Application.Identity;
using FixFlow.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace FixFlow.API.IntegrationTests.Persistence;

/// <summary>
/// <c>OwnAccount</c> and <c>OwnMemberships</c>: the caller's own rows, read with or without an organization context
/// (<c>/me</c>, T1.5).
/// </summary>
public class OwnAccountQueryTests(FixFlowApiFactory api)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task OwnMemberships_TwoMembersOfOneOrganization_EachSeesOnlyTheirOwn()
    {
        var organization = await SaveOrganizationAsync();
        var first = await SaveUserAsync();
        var second = await SaveUserAsync();
        var firstMembership = await SaveMembershipAsync(organization, first);
        var secondMembership = await SaveMembershipAsync(organization, second);

        // In the organization's context, where the organization filter alone would let both through
        Assert.Equal([firstMembership.Id], await OwnMembershipIdsAsync(new(first.Id, organization.Id)));
        Assert.Equal([secondMembership.Id], await OwnMembershipIdsAsync(new(second.Id, organization.Id)));
    }

    [Fact]
    public async Task OwnMemberships_MemberOfTwoOrganizations_SeesBothWithOrWithoutAContext()
    {
        var organizationA = await SaveOrganizationAsync();
        var organizationB = await SaveOrganizationAsync();
        var user = await SaveUserAsync();
        var inA = await SaveMembershipAsync(organizationA, user);
        var inB = await SaveMembershipAsync(organizationB, user);

        Guid[] both = [inA.Id, inB.Id];
        Assert.Equal(both.Order(), await OwnMembershipIdsAsync(new(user.Id, OrganizationId: null)));
        Assert.Equal(both.Order(), await OwnMembershipIdsAsync(new(user.Id, organizationA.Id)));
    }

    [Fact]
    public async Task OwnQueries_AnonymousCaller_ReturnNoRows()
    {
        var organization = await SaveOrganizationAsync();
        var user = await SaveUserAsync();
        await SaveMembershipAsync(organization, user);

        // Like signup: anonymous, inside the new organization's context
        var anonymous = new TestCurrentUser(UserId: null, organization.Id);

        Assert.Empty(await OwnMembershipIdsAsync(anonymous));
        Assert.Empty(await OwnAccountIdsAsync(anonymous));
    }

    [Fact]
    public async Task OwnAccount_SignedIn_ReturnsTheCallersRowOnly()
    {
        var user = await SaveUserAsync();
        await SaveUserAsync();

        Assert.Equal([user.Id], await OwnAccountIdsAsync(new(user.Id, OrganizationId: null)));
    }

    /// <summary>Sorted here, not in SQL, so they compare with ids sorted the same way.</summary>
    private async Task<List<Guid>> OwnMembershipIdsAsync(TestCurrentUser caller)
    {
        await using var scope = api.Persistence.CreateScope(caller);
        var ids = await scope.Db.OwnMemberships.Select(m => m.Id).ToListAsync(_ct);
        return [.. ids.Order()];
    }

    private async Task<List<Guid>> OwnAccountIdsAsync(TestCurrentUser caller)
    {
        await using var scope = api.Persistence.CreateScope(caller);
        return await scope.Db.OwnAccount.Select(u => u.Id).ToListAsync(_ct);
    }

    private async Task<Organization> SaveOrganizationAsync()
    {
        await using var scope = api.Persistence.CreateScope(TestCurrentUser.WithoutOrganization());
        var organization = Organization.Create(TestData.Name("Org"), TestData.OrganizationSlug());
        scope.Db.Organizations.Add(organization);
        await scope.Db.SaveChangesAsync(_ct);
        return organization;
    }

    private async Task<User> SaveUserAsync()
    {
        // Not organization-owned, so no organization context is needed
        await using var scope = api.Persistence.CreateScope(TestCurrentUser.WithoutOrganization());
        var user = new User(TestData.Email(), "Test", "User");
        scope.Db.Users.Add(user);
        await scope.Db.SaveChangesAsync(_ct);
        return user;
    }

    private async Task<OrganizationMembership> SaveMembershipAsync(Organization organization, User user)
    {
        await using var scope = api.Persistence.CreateScope(new TestCurrentUser(Guid.CreateVersion7(), organization.Id));
        var membership = OrganizationMembership.Create(user.Id, [Role.Admin], FixFlowApiFactory.SharedNow);
        scope.Db.OrganizationMemberships.Add(membership);
        await scope.Db.SaveChangesAsync(_ct);
        return membership;
    }
}
