using System.Collections.Frozen;
using FixFlow.Application.Auth;
using FixFlow.Domain.Properties;

namespace FixFlow.Application.Properties;

/// <summary>
/// The record check on units (§8): Managers and Admins may do every <see cref="UnitOps"/> operation on their
/// organization's units. Anyone else may do none; T4.3 lets tenants see their own units, but change none.
/// </summary>
internal sealed class UnitAuthorizationHandler(ICurrentUser currentUser) : IResourceAuthorizationHandler<Unit>
{
    private static readonly FrozenSet<Operation> _operations = [UnitOps.Rename, UnitOps.Archive];

    public ValueTask<bool> IsAllowedAsync(Unit resource, Operation operation, CancellationToken cancellationToken) =>
        ValueTask.FromResult(PropertyQueryExtensions.ManagesProperties(currentUser) && _operations.Contains(operation));
}
