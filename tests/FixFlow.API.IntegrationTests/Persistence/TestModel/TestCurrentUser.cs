using System.Collections.Frozen;
using FixFlow.Application.Auth;
using FixFlow.Domain.Organizations;

namespace FixFlow.API.IntegrationTests.Persistence.TestModel;

/// <summary>A caller for persistence tests, where there is no HTTP request to take one from.</summary>
public sealed record TestCurrentUser(Guid? UserId, Guid? OrganizationId) : ICurrentUser
{
    /// <summary>None by default: the persistence conventions don't depend on roles.</summary>
    public IReadOnlySet<Role> Roles { get; init; } = FrozenSet<Role>.Empty;

    /// <summary>A new user in a new organization: nobody else's data is visible to it.</summary>
    public static TestCurrentUser InNewOrganization() => new(Guid.CreateVersion7(), Guid.CreateVersion7());

    /// <summary>
    /// A new user in a saved <paramref name="organization"/> (<see cref="PersistenceTestDatabase.SaveOrganizationAsync"/>),
    /// as the organization context opens it: for rows with a foreign key to their organization.
    /// </summary>
    public static TestCurrentUser InOrganization(Organization organization) => new(Guid.CreateVersion7(), organization.Id);

    /// <summary>Signed in, outside any organization (like <c>/me</c>).</summary>
    public static TestCurrentUser WithoutOrganization() => new(Guid.CreateVersion7(), null);

    /// <summary>Another user of the same organization.</summary>
    public TestCurrentUser Colleague() => this with { UserId = Guid.CreateVersion7() };
}
