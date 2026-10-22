using FixFlow.Application.Auth;

namespace FixFlow.API.IntegrationTests.Persistence.TestModel;

/// <summary>A caller for persistence tests, where there is no HTTP request to take one from.</summary>
public sealed record TestCurrentUser(Guid? UserId, Guid? OrganizationId) : ICurrentUser
{
    /// <summary>A new user in a new organization: nobody else's data is visible to it.</summary>
    public static TestCurrentUser InNewOrganization() => new(Guid.CreateVersion7(), Guid.CreateVersion7());

    /// <summary>Signed in, outside any organization (like <c>/me</c>).</summary>
    public static TestCurrentUser WithoutOrganization() => new(Guid.CreateVersion7(), null);

    /// <summary>Another user of the same organization.</summary>
    public TestCurrentUser Colleague() => this with { UserId = Guid.CreateVersion7() };
}
