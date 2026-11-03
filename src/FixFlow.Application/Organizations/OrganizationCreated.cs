using FixFlow.Domain.Organizations;

namespace FixFlow.Application.Organizations;

/// <summary>
/// A new organization, as signup and <c>POST /organizations</c> return it: the SPA goes on to <c>/{Slug}</c>.
/// </summary>
public sealed record OrganizationCreated(Guid Id, string Name, string Slug)
{
    internal static OrganizationCreated From(Organization organization) =>
        new(organization.Id, organization.Name, organization.Slug);
}
