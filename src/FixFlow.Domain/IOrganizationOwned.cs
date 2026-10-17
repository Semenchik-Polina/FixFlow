namespace FixFlow.Domain;

/// <summary>
/// Data owned by one organization (D2). Persistence sets <see cref="OrganizationId"/> from the request's organization
/// context on insert, refuses to save it for any other organization, and filters every query by it.
/// </summary>
public interface IOrganizationOwned
{
    Guid OrganizationId { get; }
}
