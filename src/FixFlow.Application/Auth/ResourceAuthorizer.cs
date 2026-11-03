using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FixFlow.Application.Auth;

/// <summary>
/// <see cref="IResourceAuthorizer"/> over the resources' handlers. Scoped, with the request's current user and handlers.
/// </summary>
internal sealed partial class ResourceAuthorizer(
    IServiceProvider services,
    ICurrentUser currentUser,
    ILogger<ResourceAuthorizer> logger) : IResourceAuthorizer
{
    public async ValueTask<bool> IsAllowedAsync<TResource>(
        TResource resource, Operation operation, CancellationToken cancellationToken)
        where TResource : class
    {
        // Looked up first, so a missing handler fails every call, not only the ones that reach it
        var handler = services.GetServices<IResourceAuthorizationHandler<TResource>>().ToList() is [var single]
            ? single
            : throw new InvalidOperationException(
                $"{typeof(TResource).Name} needs exactly one {nameof(IResourceAuthorizationHandler<>)}.");

        // Every operation is a member's, in an organization, with a role there
        var allowed = currentUser.OrganizationId is not null
            && currentUser.Roles.Count > 0
            && await handler.IsAllowedAsync(resource, operation, cancellationToken);

        if (!allowed)
        {
            // The caller gets a 404 that doesn't say why; this does, with ids only (S26)
            LogDenied(operation.Name, typeof(TResource).Name, currentUser.UserId);
        }

        return allowed;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Operation {Operation} on a {ResourceType} denied to user {UserId}")]
    private partial void LogDenied(string operation, string resourceType, Guid? userId);
}
