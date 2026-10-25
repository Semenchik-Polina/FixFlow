using FixFlow.Application.Auth;

namespace FixFlow.API.Auth;

/// <summary>
/// The request's <see cref="ITrustedOrganizationScope"/> (scoped). <see cref="HttpCurrentUser"/> reads its open id
/// through this class, not through <see cref="ICurrentUser"/>, so casting the current user can't open a scope.
/// </summary>
internal sealed class TrustedOrganizationScope(IHttpContextAccessor httpContextAccessor) : ITrustedOrganizationScope
{
    /// <summary>The open scope's organization; null when no scope is open.</summary>
    public Guid? OrganizationId { get; private set; }

    public IDisposable Open(Guid organizationId)
    {
        if (organizationId == Guid.Empty)
        {
            throw new InvalidOperationException("A trusted organization scope needs an organization id.");
        }

        if (OrganizationId is not null)
        {
            throw new InvalidOperationException("A trusted organization scope is already open.");
        }

        // The scope is for callers without a context; it must never override the one the request resolved
        if (httpContextAccessor.HttpContext?.User.FindFirst(FixFlowClaimTypes.OrganizationId) is not null)
        {
            throw new InvalidOperationException("The request already has an organization context.");
        }

        OrganizationId = organizationId;
        return new Closer(this);
    }

    /// <summary>Closes its own scope once. A second dispose does nothing, even after another scope was opened.</summary>
    private sealed class Closer(TrustedOrganizationScope scope) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            scope.OrganizationId = null;
        }
    }
}
