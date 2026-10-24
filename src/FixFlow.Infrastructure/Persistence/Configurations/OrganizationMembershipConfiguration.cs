using FixFlow.Application.Identity;
using FixFlow.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FixFlow.Infrastructure.Persistence.Configurations;

internal sealed class OrganizationMembershipConfiguration : IEntityTypeConfiguration<OrganizationMembership>
{
    public void Configure(EntityTypeBuilder<OrganizationMembership> builder)
    {
        builder.ToTable("organization_memberships", Schemas.Organizations);

        // One membership per user and organization (§10). Led by the user: resolving the organization context (T1.5)
        // and /me look a user's memberships up.
        builder.HasIndex(m => new { m.UserId, m.OrganizationId }).IsUnique();

        // Deleting users or organizations is out of the MVP, so nothing cascades into memberships
        builder.HasOne<Organization>().WithMany().HasForeignKey(m => m.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);

        // Owned, so a role can only be read through its membership, which the organization filter covers. The
        // membership and the role are its key.
        builder.OwnsMany(m => m.Roles, roles =>
        {
            roles.ToTable("membership_roles", Schemas.Organizations);
            roles.WithOwner().HasForeignKey("MembershipId");
            roles.HasKey("MembershipId", nameof(MembershipRole.Role));
        });
    }
}
