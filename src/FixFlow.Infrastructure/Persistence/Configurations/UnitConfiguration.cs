using FixFlow.Application.Persistence;
using FixFlow.Domain.Organizations;
using FixFlow.Domain.Properties;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FixFlow.Infrastructure.Persistence.Configurations;

internal sealed class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> builder)
    {
        builder.ToTable("units", Schemas.Properties);

        // Case-insensitive, so the unique index below takes "4B" and "4b" for one name
        builder.Property(u => u.Name).HasMaxLength(Unit.NameMaxLength).UseCollation(Collations.CaseInsensitive);

        // One name per property among the units that aren't archived, so an archived unit's name can be used again.
        // Led by the property, whose units are listed together.
        builder.HasIndex(u => new { u.PropertyId, u.Name })
            .IsUnique()
            .HasFilter("deleted_at IS NULL")
            .HasDatabaseName(UniqueIndexes.UnitName);

        // Properties are archived and organizations aren't deleted in the MVP, so nothing cascades
        builder.HasOne<Property>().WithMany().HasForeignKey(u => u.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Organization>().WithMany().HasForeignKey(u => u.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}
