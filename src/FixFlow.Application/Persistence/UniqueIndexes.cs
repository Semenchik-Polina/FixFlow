namespace FixFlow.Application.Persistence;

/// <summary>
/// The names of the unique indexes that handlers map to field errors when a save violates them (plan P27,
/// <see cref="Errors.UniqueConstraintViolationException"/>). The configurations name the indexes with these, so a rename
/// here needs a migration.
/// </summary>
public static class UniqueIndexes
{
    /// <summary>One account per email (D5), compared normalized.</summary>
    public const string UserEmail = "ix_users_normalized_email";

    /// <summary>A slug is unique across all organizations (§10).</summary>
    public const string OrganizationSlug = "ix_organizations_slug";

    /// <summary>One membership per user and organization (§10).</summary>
    public const string MembershipUserOrganization = "ix_organization_memberships_user_id_organization_id";
}
