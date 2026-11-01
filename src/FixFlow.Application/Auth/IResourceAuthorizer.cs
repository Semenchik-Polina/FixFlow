namespace FixFlow.Application.Auth;

/// <summary>
/// The record layer of authorization (§8), after the endpoint's role policy: may the caller perform an
/// <see cref="Operation"/> on this record? A handler loads the record, asks, and answers <c>Result.NotFound()</c> on a
/// no, so a record the caller may not touch looks like one that doesn't exist (D29).
/// </summary>
public interface IResourceAuthorizer
{
    /// <summary>
    /// Asks the resource's <see cref="IResourceAuthorizationHandler{TResource}"/>. Without an organization context, or
    /// without a role in it (an open trusted scope gives none, plan P25), the answer is no and the handler isn't asked.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="TResource"/> has no handler, or more than one: a bug, never a denial.
    /// </exception>
    ValueTask<bool> IsAllowedAsync<TResource>(TResource resource, Operation operation, CancellationToken cancellationToken)
        where TResource : class;
}
