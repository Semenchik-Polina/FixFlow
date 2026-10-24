using FixFlow.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FixFlow.Infrastructure.Persistence.Configurations;

internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organizations", Schemas.Organizations);

        builder.Property(o => o.Name).HasMaxLength(Organization.NameMaxLength);
        builder.Property(o => o.Slug).HasMaxLength(OrganizationSlug.MaxLength);

        // Unique across all organizations (§10). The aggregate can't check that; signup turns a violation into a field
        // error (T1.3).
        builder.HasIndex(o => o.Slug).IsUnique();
    }
}
