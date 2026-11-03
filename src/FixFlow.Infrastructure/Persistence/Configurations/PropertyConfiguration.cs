using FixFlow.Domain.Organizations;
using FixFlow.Domain.Properties;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FixFlow.Infrastructure.Persistence.Configurations;

internal sealed class PropertyConfiguration : IEntityTypeConfiguration<Property>
{
    public void Configure(EntityTypeBuilder<Property> builder)
    {
        builder.ToTable("properties", Schemas.Properties);

        builder.Property(p => p.Name).HasMaxLength(Property.NameMaxLength);
        builder.Property(p => p.TimeZoneId).HasMaxLength(PropertyTimeZone.MaxLength);

        // A value without an identity: its parts are columns of the property's row (address_line1, ...)
        builder.ComplexProperty(p => p.Address, address =>
        {
            address.Property(a => a.Line1).HasMaxLength(Address.LineMaxLength);
            address.Property(a => a.Line2).HasMaxLength(Address.LineMaxLength);
            address.Property(a => a.City).HasMaxLength(Address.CityMaxLength);
            address.Property(a => a.PostalCode).HasMaxLength(Address.PostalCodeMaxLength);
            address.Property(a => a.CountryCode).HasMaxLength(Address.CountryCodeLength);
        });

        // Organizations aren't deleted in the MVP, so nothing cascades
        builder.HasOne<Organization>().WithMany().HasForeignKey(p => p.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}
