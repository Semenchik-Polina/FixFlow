using System.Collections.Frozen;
using FixFlow.Application.Auth;
using FixFlow.Domain.Properties;

namespace FixFlow.Application.Properties;

/// <summary>
/// The record check on properties (§8): Managers and Admins may do every <see cref="PropertyOps"/> operation on their
/// organization's properties, which are the only ones the organization filter lets them load. Anyone else may do none.
/// </summary>
internal sealed class PropertyAuthorizationHandler(ICurrentUser currentUser) : IResourceAuthorizationHandler<Property>
{
    private static readonly FrozenSet<Operation> _operations = [PropertyOps.Update, PropertyOps.Archive, PropertyOps.AddUnit];

    public ValueTask<bool> IsAllowedAsync(Property resource, Operation operation, CancellationToken cancellationToken) =>
        ValueTask.FromResult(PropertyQueryExtensions.ManagesProperties(currentUser) && _operations.Contains(operation));
}
