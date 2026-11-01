namespace FixFlow.Application.Auth;

/// <summary>
/// Something a caller does to a record, checked by <see cref="IResourceAuthorizer"/> (§8): <c>WorkOrderOps.Assign</c>,
/// <c>WorkOrderOps.UpdateStatus</c>. Checks name operations, not roles, so the rules can move to a permission model
/// without touching the handlers that ask. A module defines its operations in one static class (<c>WorkOrderOps</c>),
/// named <c>"{Resource}.{Action}"</c>.
/// </summary>
public sealed record Operation(string Name)
{
    public override string ToString() => Name;
}
