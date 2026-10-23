using FixFlow.Domain.Organizations;
using NodaTime;

namespace FixFlow.Domain.Tests.Organizations;

public class OrganizationMembershipTests
{
    private static readonly Guid _userId = Guid.CreateVersion7();

    private static readonly Instant _joinedAt = Instant.FromUtc(2026, 10, 23, 9, 30);

    [Fact]
    public void Create_ValidArguments_IsActiveWithRoles()
    {
        var membership = OrganizationMembership.Create(_userId, [Role.Manager, Role.Technician], _joinedAt);

        Assert.Equal(_userId, membership.UserId);
        Assert.Equal(MembershipStatus.Active, membership.Status);
        Assert.Equal(_joinedAt, membership.JoinedAt);
        Assert.Equal([Role.Technician, Role.Manager], RolesOf(membership));
    }

    [Fact]
    public void Create_ValidArguments_LeavesOrganizationIdToPersistence() =>
        Assert.Equal(Guid.Empty, OrganizationMembership.Create(_userId, [Role.Tenant], _joinedAt).OrganizationId);

    [Fact]
    public void Create_DuplicateRoles_CountOnce()
    {
        var membership = OrganizationMembership.Create(_userId, [Role.Admin, Role.Tenant, Role.Admin], _joinedAt);

        Assert.Equal([Role.Tenant, Role.Admin], RolesOf(membership));
    }

    [Fact]
    public void Create_NoRoles_Throws()
    {
        var exception = Assert.Throws<DomainException>(() => OrganizationMembership.Create(_userId, [], _joinedAt));
        Assert.Equal("A member needs at least one role.", exception.Message);
    }

    [Fact]
    public void Create_UndefinedRole_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => OrganizationMembership.Create(_userId, [Role.Manager, (Role)42], _joinedAt));

    [Fact]
    public void Create_EmptyUserId_Throws() =>
        Assert.Throws<ArgumentException>(() => OrganizationMembership.Create(Guid.Empty, [Role.Tenant], _joinedAt));

    [Fact]
    public void Create_NullRoles_Throws() =>
        Assert.Throws<ArgumentNullException>(() => OrganizationMembership.Create(_userId, null!, _joinedAt));

    [Theory]
    [InlineData(Role.Technician, true)]
    [InlineData(Role.Admin, true)]
    [InlineData(Role.Tenant, false)]
    // Admin includes Manager in the policies, not in the membership
    [InlineData(Role.Manager, false)]
    public void HasRole_TechnicianAndAdmin_HasOnlyThose(Role role, bool expected)
    {
        var membership = OrganizationMembership.Create(_userId, [Role.Technician, Role.Admin], _joinedAt);

        Assert.Equal(expected, membership.HasRole(role));
    }

    [Fact]
    public void ChangeRoles_NewRoles_ReplacesRoles()
    {
        var membership = OrganizationMembership.Create(_userId, [Role.Tenant, Role.Technician], _joinedAt);

        membership.ChangeRoles([Role.Technician, Role.Manager]);

        Assert.Equal([Role.Technician, Role.Manager], RolesOf(membership));
    }

    [Fact]
    public void ChangeRoles_RoleThatStays_KeepsItsEntry()
    {
        var membership = OrganizationMembership.Create(_userId, [Role.Tenant, Role.Technician], _joinedAt);
        var technician = membership.Roles.Single(r => r.Role == Role.Technician);

        membership.ChangeRoles([Role.Technician, Role.Manager]);

        Assert.Same(technician, membership.Roles.Single(r => r.Role == Role.Technician));
    }

    [Fact]
    public void ChangeRoles_NoRoles_ThrowsAndKeepsRoles()
    {
        var membership = OrganizationMembership.Create(_userId, [Role.Manager], _joinedAt);

        Assert.Throws<DomainException>(() => membership.ChangeRoles([]));
        Assert.Equal([Role.Manager], RolesOf(membership));
    }

    [Fact]
    public void ChangeRoles_UndefinedRole_ThrowsAndKeepsRoles()
    {
        var membership = OrganizationMembership.Create(_userId, [Role.Manager], _joinedAt);

        Assert.Throws<ArgumentOutOfRangeException>(() => membership.ChangeRoles([Role.Admin, (Role)42]));
        Assert.Equal([Role.Manager], RolesOf(membership));
    }

    [Fact]
    public void Suspend_Active_Suspends()
    {
        var membership = OrganizationMembership.Create(_userId, [Role.Manager], _joinedAt);

        membership.Suspend();

        Assert.Equal(MembershipStatus.Suspended, membership.Status);
    }

    [Fact]
    public void Suspend_Suspended_Throws()
    {
        var membership = OrganizationMembership.Create(_userId, [Role.Manager], _joinedAt);
        membership.Suspend();

        var exception = Assert.Throws<DomainException>(membership.Suspend);
        Assert.Equal("This member is already suspended.", exception.Message);
    }

    [Fact]
    public void Reactivate_Suspended_ReactivatesWithSameRoles()
    {
        var membership = OrganizationMembership.Create(_userId, [Role.Technician, Role.Manager], _joinedAt);
        membership.Suspend();

        membership.Reactivate();

        Assert.Equal(MembershipStatus.Active, membership.Status);
        Assert.Equal([Role.Technician, Role.Manager], RolesOf(membership));
    }

    [Fact]
    public void Reactivate_Active_Throws()
    {
        var membership = OrganizationMembership.Create(_userId, [Role.Manager], _joinedAt);

        var exception = Assert.Throws<DomainException>(membership.Reactivate);
        Assert.Equal("This member is already active.", exception.Message);
    }

    private static Role[] RolesOf(OrganizationMembership membership) =>
        [.. membership.Roles.Select(r => r.Role).Order()];
}
