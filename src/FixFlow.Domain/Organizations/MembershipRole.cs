namespace FixFlow.Domain.Organizations;

/// <summary>
/// One role of an <see cref="OrganizationMembership"/> (§7). It belongs to the membership and is reached only through
/// it, so it needs no id or organization id of its own: the membership and the role are its key.
/// </summary>
public sealed class MembershipRole
{
    internal MembershipRole(Role role) => Role = role;

    // For EF Core
    private MembershipRole()
    {
    }

    public Role Role { get; private set; }
}
