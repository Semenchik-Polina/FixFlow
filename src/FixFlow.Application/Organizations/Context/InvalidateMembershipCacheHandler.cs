using FixFlow.Application.Events;
using FixFlow.Domain.Organizations;
using Microsoft.Extensions.Caching.Hybrid;

namespace FixFlow.Application.Organizations.Context;

/// <summary>
/// Drops the cached memberships of a user whose membership changed, once the change is committed, so it applies to
/// their next request rather than after <see cref="MembershipCache.Expiration"/> (R15). Every change to a membership
/// raises one of these events, so no endpoint handler has to remember to invalidate.
/// </summary>
internal sealed class InvalidateMembershipCacheHandler(HybridCache cache) :
    IPostCommitEventHandler<MembershipCreated>,
    IPostCommitEventHandler<MembershipRolesChanged>,
    IPostCommitEventHandler<MembershipSuspended>,
    IPostCommitEventHandler<MembershipReactivated>
{
    // A new membership too: the user may have asked for that organization while they weren't a member yet, and the
    // miss is cached
    public Task Handle(MembershipCreated domainEvent, CancellationToken cancellationToken) =>
        InvalidateAsync(domainEvent.UserId, cancellationToken);

    public Task Handle(MembershipRolesChanged domainEvent, CancellationToken cancellationToken) =>
        InvalidateAsync(domainEvent.UserId, cancellationToken);

    public Task Handle(MembershipSuspended domainEvent, CancellationToken cancellationToken) =>
        InvalidateAsync(domainEvent.UserId, cancellationToken);

    public Task Handle(MembershipReactivated domainEvent, CancellationToken cancellationToken) =>
        InvalidateAsync(domainEvent.UserId, cancellationToken);

    private Task InvalidateAsync(Guid userId, CancellationToken cancellationToken) =>
        cache.RemoveByTagAsync(MembershipCache.Tag(userId), cancellationToken).AsTask();
}
