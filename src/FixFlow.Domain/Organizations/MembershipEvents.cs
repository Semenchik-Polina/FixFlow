namespace FixFlow.Domain.Organizations;

// A membership decides which organizations its user may enter and with which roles, so every change to it is an
// event: the cache of the user's memberships is invalidated after the commit (T1.5), and the audit log records it (T9.2).
// The organization id isn't carried: a new membership doesn't have it until persistence sets it, and the membership
// id leads to it.

/// <summary>A user joined an organization: signup, a new organization, or an accepted invite.</summary>
public sealed record MembershipCreated(Guid MembershipId, Guid UserId) : IDomainEvent;

/// <summary>A member's roles changed; not raised when the new set is the same.</summary>
public sealed record MembershipRolesChanged(Guid MembershipId, Guid UserId) : IDomainEvent;

/// <summary>A member lost access to the organization.</summary>
public sealed record MembershipSuspended(Guid MembershipId, Guid UserId) : IDomainEvent;

/// <summary>A suspended member got access back.</summary>
public sealed record MembershipReactivated(Guid MembershipId, Guid UserId) : IDomainEvent;
