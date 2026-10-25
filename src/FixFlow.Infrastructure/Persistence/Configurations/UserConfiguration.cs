using FixFlow.Application.Identity;
using FixFlow.Application.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FixFlow.Infrastructure.Persistence.Configurations;

/// <summary>Identity's user table, renamed into the identity schema (§10), plus the account's own columns.</summary>
internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", Schemas.Identity);

        // User isn't an Entity, so the convention doesn't cover it: the constructor assigns a UUIDv7 (D10)
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.Property(u => u.Email).IsRequired().HasMaxLength(User.EmailMaxLength);
        builder.Property(u => u.FirstName).HasMaxLength(User.NameMaxLength);
        builder.Property(u => u.LastName).HasMaxLength(User.NameMaxLength);

        // Identity names these indexes itself, and leaves the email's non-unique. Sign-in is by email, so two accounts
        // with one email would make it ambiguous (§10).
        builder.HasIndex(u => u.NormalizedUserName).HasDatabaseName("ix_users_normalized_user_name");
        builder.HasIndex(u => u.NormalizedEmail).IsUnique().HasDatabaseName(UniqueIndexes.UserEmail);
    }
}
