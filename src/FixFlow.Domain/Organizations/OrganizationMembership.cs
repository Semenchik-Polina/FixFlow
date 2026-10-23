using NodaTime;

namespace FixFlow.Domain.Organizations;

/// <summary>
/// A user's access to one organization, with their roles there (D4, §8). A user can be a member of several
/// organizations, with different roles in each. Only an active membership gives access.
/// </summary>
public sealed class OrganizationMembership : AggregateRoot, IOrganizationOwned, IAuditable
{
    private readonly List<MembershipRole> _roles = [];

    private OrganizationMembership(Guid userId, Instant joinedAt)
    {
        UserId = userId;
        JoinedAt = joinedAt;
        Status = MembershipStatus.Active;
    }

    // For EF Core
    private OrganizationMembership()
    {
    }

    public Guid UserId { get; private set; }

    public MembershipStatus Status { get; private set; }

    public Instant JoinedAt { get; private set; }

    /// <summary>At least one, without duplicates.</summary>
    public IReadOnlyCollection<MembershipRole> Roles => _roles;

    public Guid OrganizationId { get; private set; }

    public Instant CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public Instant? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    /// <summary>
    /// An active membership. Persistence sets <see cref="OrganizationId"/> from the organization context, which signup
    /// and invite acceptance open for the new member's organization (T1.5).
    /// </summary>
    /// <param name="userId">The member.</param>
    /// <param name="roles">At least one; duplicates count once.</param>
    /// <param name="joinedAt">When the user joined: signed up or accepted the invite.</param>
    /// <exception cref="DomainException">No roles were given.</exception>
    public static OrganizationMembership Create(Guid userId, IEnumerable<Role> roles, Instant joinedAt)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A member needs a user id.", nameof(userId));
        }

        var membership = new OrganizationMembership(userId, joinedAt);
        membership.ChangeRoles(roles);
        return membership;
    }

    public bool HasRole(Role role) => _roles.Exists(r => r.Role == role);

    /// <summary>Gives the member exactly these roles. Nothing changes if they aren't valid.</summary>
    /// <param name="roles">At least one; duplicates count once.</param>
    /// <exception cref="DomainException">No roles were given.</exception>
    public void ChangeRoles(IEnumerable<Role> roles)
    {
        ArgumentNullException.ThrowIfNull(roles);

        var wanted = roles.ToHashSet();
        foreach (var role in wanted)
        {
            // The API reads roles by name only, so a value outside the enum can only come from code
            if (!Enum.IsDefined(role))
            {
                throw new ArgumentOutOfRangeException(nameof(roles), role, "Not a role.");
            }
        }

        if (wanted.Count == 0)
        {
            throw new DomainException("A member needs at least one role.");
        }

        // Keep the roles that stay rather than replacing them all: removing a role and adding it back in the same save
        // would be two rows with one key (membership, role)
        _roles.RemoveAll(r => !wanted.Contains(r.Role));
        foreach (var role in wanted.Where(role => !HasRole(role)))
        {
            _roles.Add(new MembershipRole(role));
        }
    }

    /// <summary>Takes away the member's access to the organization; their roles are kept for a reactivation.</summary>
    /// <exception cref="DomainException">The membership is already suspended.</exception>
    public void Suspend()
    {
        if (Status == MembershipStatus.Suspended)
        {
            throw new DomainException("This member is already suspended.");
        }

        Status = MembershipStatus.Suspended;
    }

    /// <summary>Gives a suspended member their access back, with the roles they had.</summary>
    /// <exception cref="DomainException">The membership is already active.</exception>
    public void Reactivate()
    {
        if (Status == MembershipStatus.Active)
        {
            throw new DomainException("This member is already active.");
        }

        Status = MembershipStatus.Active;
    }
}
