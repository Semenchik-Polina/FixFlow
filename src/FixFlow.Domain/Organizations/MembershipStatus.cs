namespace FixFlow.Domain.Organizations;

/// <summary>A suspended member keeps the membership and its roles, but has no access to the organization (§8).</summary>
public enum MembershipStatus
{
    Active,
    Suspended,
}
