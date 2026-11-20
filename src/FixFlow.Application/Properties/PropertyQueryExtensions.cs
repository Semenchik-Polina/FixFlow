using System.Collections.Frozen;
using FixFlow.Application.Auth;
using FixFlow.Application.Persistence;
using FixFlow.Domain.Organizations;
using FixFlow.Domain.Properties;
using Microsoft.EntityFrameworkCore;

namespace FixFlow.Application.Properties;

/// <summary>
/// Which properties and units the caller sees (§8), for lists and detail reads alike. Managers and Admins see all of
/// their organization's; nobody else sees any yet. Later slices add a tenant's own units (T4.3) and a technician's.
/// </summary>
public static class PropertyQueryExtensions
{
    private static readonly FrozenSet<Role> _managers = [Role.Manager, Role.Admin];

    public static IQueryable<Property> VisibleTo(this IQueryable<Property> properties, ICurrentUser user) =>
        ManagesProperties(user) ? properties : properties.Where(_ => false);

    public static IQueryable<Unit> VisibleTo(this IQueryable<Unit> units, ICurrentUser user) =>
        ManagesProperties(user) ? units : units.Where(_ => false);

    /// <summary>
    /// The unit <paramref name="unitId"/>, tracked, when it is in the property <paramref name="propertyId"/> and the
    /// caller sees both. The filters don't look at a unit's property (<see cref="IFixFlowDbContext.Units"/>), so the
    /// property is checked too.
    /// </summary>
    internal static Task<Unit?> FindUnitAsync(
        this IFixFlowDbContext db, ICurrentUser user, Guid propertyId, Guid unitId, CancellationToken cancellationToken)
    {
        var properties = db.Properties.VisibleTo(user);

        return db.Units
            .VisibleTo(user)
            .Where(unit => unit.Id == unitId
                && unit.PropertyId == propertyId
                && properties.Any(property => property.Id == propertyId))
            .SingleOrDefaultAsync(cancellationToken);
    }

    /// <summary>Managers and Admins manage every property of their organization (§8).</summary>
    internal static bool ManagesProperties(ICurrentUser user) => user.Roles.Overlaps(_managers);
}
