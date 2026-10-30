using System.Collections.Immutable;
using System.ComponentModel;
using FixFlow.Domain.Organizations;

namespace FixFlow.Application.Organizations.Context;

/// <summary>The caller's access to the organization a request names: its id and the caller's roles there.</summary>
/// <param name="Roles">At least one, in the order of <see cref="Role"/>.</param>
// Immutable, so the in-memory cache hands out the instance it holds instead of a deserialized copy per request
[ImmutableObject(true)]
public sealed record ActiveMembership(Guid OrganizationId, ImmutableArray<Role> Roles);
