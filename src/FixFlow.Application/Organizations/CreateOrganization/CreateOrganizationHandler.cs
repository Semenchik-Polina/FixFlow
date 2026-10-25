using FixFlow.Application.Auth;
using FixFlow.Application.Results;
using FixFlow.Domain.Organizations;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FixFlow.Application.Organizations.CreateOrganization;

/// <summary>
/// Creates the organization inside its own trusted scope (plan P25): the caller has no organization context, and the
/// id is the one just created, never one from the request.
/// </summary>
internal sealed partial class CreateOrganizationHandler(
    OrganizationCreator creator,
    ICurrentUser currentUser,
    ITrustedOrganizationScope trustedScope,
    ILogger<CreateOrganizationHandler> logger) : IRequestHandler<CreateOrganizationCommand, Result<OrganizationCreated>>
{
    public async Task<Result<OrganizationCreated>> Handle(
        CreateOrganizationCommand command, CancellationToken cancellationToken)
    {
        var founderId = currentUser.UserId
            ?? throw new InvalidOperationException("Creating an organization needs a signed-in user.");

        var organization = Organization.Create(command.Name, command.Slug);

        // No explicit transaction: the creator's single save commits and dispatches its post-commit events inside
        // the scope
        using (trustedScope.Open(organization.Id))
        {
            var created = await creator.CreateAsync(organization, founderId, cancellationToken);
            if (!created.IsSuccess)
            {
                return created.Error;
            }
        }

        LogCreated(founderId, organization.Id);
        return OrganizationCreated.From(organization);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} created organization {OrganizationId}")]
    private partial void LogCreated(Guid userId, Guid organizationId);
}
