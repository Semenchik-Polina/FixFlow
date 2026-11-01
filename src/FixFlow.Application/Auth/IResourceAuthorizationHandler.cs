namespace FixFlow.Application.Auth;

/// <summary>
/// Decides which <see cref="Operation"/>s the caller may perform on a loaded <typeparamref name="TResource"/>: the
/// record layer of §8, one handler per resource, next to its operations (<c>WorkOrderAuthorizationHandler</c>). It
/// reads the caller only from <see cref="ICurrentUser"/>, and may query through <c>IFixFlowDbContext</c> when the
/// record alone can't tell (a tenant's units). An operation it doesn't know is denied.
/// <c>AddResourceAuthorizationHandlers</c> registers it, and handlers ask through <see cref="IResourceAuthorizer"/>.
/// </summary>
/// <remarks>
/// Viewing is no operation here: reads load through the resource's <c>VisibleTo(user)</c>, the filter its lists use,
/// so a list and a detail can't disagree about what the caller sees.
/// </remarks>
public interface IResourceAuthorizationHandler<TResource>
    where TResource : class
{
    /// <summary>Called only in an organization context, for a caller with at least one role there.</summary>
    ValueTask<bool> IsAllowedAsync(TResource resource, Operation operation, CancellationToken cancellationToken);
}
