using System.Collections.Frozen;
using FixFlow.Domain.Organizations;

namespace FixFlow.API.Auth;

/// <summary>
/// The role policies of the endpoint layer (§8): <c>.RequireAuthorization(Policies.CanManageProperties)</c>. Each passes
/// when the caller has one of its roles in the request's organization, read from <c>ICurrentUser.Roles</c>
/// (<see cref="OrganizationRoleHandler"/>); a caller without the role gets a 403. Every endpoint that needs an
/// organization names one of them, which <c>EndpointCoverageTests</c> checks. The record layer comes after it, in the
/// handler: a record the caller may not see is a 404.
/// </summary>
public static class Policies
{
    /// <summary>The organization's settings, members' roles and suspensions (A9).</summary>
    public const string CanManageOrganization = nameof(CanManageOrganization);

    /// <summary>Invites, the member list and tenants' units.</summary>
    public const string CanManageMembers = nameof(CanManageMembers);

    // Property is a building or site, that has an address and a time zone 
    public const string CanManageProperties = nameof(CanManageProperties);

    /// <summary>Submitting and cancelling one's own requests (A13).</summary>
    public const string CanSubmitRequests = nameof(CanSubmitRequests);

    /// <summary>Accepting, rejecting and reviewing requests (§7).</summary>
    public const string CanTriageRequests = nameof(CanTriageRequests);

    /// <summary>
    /// Working on work orders: reading them, moving them through their states, adding photos. Managers do the
    /// technician's steps too (§7); the record check limits a technician to the jobs assigned to them.
    /// </summary>
    public const string CanWorkOnJobs = nameof(CanWorkOnJobs);

    /// <summary>
    /// The roles that pass each policy: the one place they are written. Admin is listed wherever Manager is, because an
    /// Admin membership doesn't also hold Manager (<see cref="Role"/>).
    /// </summary>
    public static readonly FrozenDictionary<string, FrozenSet<Role>> Roles = new Dictionary<string, FrozenSet<Role>>
    {
        [CanManageOrganization] = [Role.Admin],
        [CanManageMembers] = [Role.Manager, Role.Admin],
        [CanManageProperties] = [Role.Manager, Role.Admin],
        [CanSubmitRequests] = [Role.Tenant],
        [CanTriageRequests] = [Role.Manager, Role.Admin],
        [CanWorkOnJobs] = [Role.Technician, Role.Manager, Role.Admin],
    }.ToFrozenDictionary(StringComparer.Ordinal);
}
