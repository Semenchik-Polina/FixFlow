using System.Net;
using FixFlow.Application.Identity;
using FixFlow.Domain.Organizations;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace FixFlow.API.IntegrationTests.Fixtures;

/// <summary>
/// Members and organizations for cross-tenant tests (§19): members who join an organization directly (there are no
/// invites before T3.2), two organizations with a signed-in member per role, and one user in both with different
/// roles. A test sends a mismatched header with <see cref="HttpClientExtensions.WithOrganization(HttpRequestMessage, string)"/>.
/// </summary>
public static class CrossTenantExtensions
{
    /// <summary>
    /// A new account, signed out, that is an active member of <paramref name="organizationId"/> with
    /// <paramref name="roles"/>. The account is created through <see cref="UserManager{TUser}"/>, as signup creates
    /// one, and the membership through the aggregate (<see cref="AppDatabaseExtensions.JoinAsync"/>).
    /// </summary>
    public static async Task<TestMember> AddMemberAsync(
        this FixFlowApiFactory api, Guid organizationId, params Role[] roles)
    {
        var member = await api.CreateAccountAsync();
        await api.JoinAsync(organizationId, member.UserId, roles);
        return member;
    }

    /// <summary>
    /// A new client signed in as <paramref name="member"/>, which sends no organization header yet. The caller disposes
    /// it.
    /// </summary>
    public static async Task<HttpClient> SignInAsync(this FixFlowApiFactory api, TestMember member)
    {
        var client = api.CreateApiClient();
        using var response = await client.LogInAsync(member.Email, member.Password);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        return client;
    }

    /// <summary>Two organizations, A and B, with a signed-in member per role each. Dispose it with <c>using</c>.</summary>
    public static async Task<CrossTenant> TwoOrganizationsAsync(this FixFlowApiFactory api)
    {
        var organizations = await Task.WhenAll(api.OrganizationWithEveryRoleAsync(), api.OrganizationWithEveryRoleAsync());
        return new CrossTenant(organizations[0], organizations[1]);
    }

    /// <summary>
    /// A new account, signed out, that is an active member of both organizations, with different roles in each if the
    /// test chooses: a Manager in A and a Tenant in B, say.
    /// </summary>
    public static async Task<TestMember> MemberOfBothAsync(
        this FixFlowApiFactory api, Guid organizationA, Role[] rolesInA, Guid organizationB, Role[] rolesInB)
    {
        var member = await api.AddMemberAsync(organizationA, rolesInA);
        await api.JoinAsync(organizationB, member.UserId, rolesInB);
        return member;
    }

    private static async Task<TestMember> CreateAccountAsync(this FixFlowApiFactory api)
    {
        var user = new User(TestData.Email(), "Test", "Member");

        await using var scope = api.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<UserManager<User>>().CreateAsync(user, TestData.Password);

        // Codes only: a description can hold the email
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Code)));
        return new TestMember(user.Id, user.Email!, TestData.Password);
    }

    // The founder signs up, so the Admin's membership is the one signup creates; the others join and sign in, in parallel
    private static async Task<TestOrganization> OrganizationWithEveryRoleAsync(this FixFlowApiFactory api)
    {
        var admin = api.CreateApiClient();
        var signedUp = await admin.SignUpAsync();
        var members = await Task.WhenAll(
            new[] { Role.Manager, Role.Technician, Role.Tenant }.Select(async role =>
                await api.SignInAsync(await api.AddMemberAsync(signedUp.OrganizationId, role))));

        return new TestOrganization(
            signedUp.OrganizationId,
            signedUp.Slug,
            admin.WithOrganization(signedUp.Slug),
            members[0].WithOrganization(signedUp.Slug),
            members[1].WithOrganization(signedUp.Slug),
            members[2].WithOrganization(signedUp.Slug));
    }
}

/// <summary>An account made by <see cref="CrossTenantExtensions.AddMemberAsync"/>: sign it in with <c>api.SignInAsync</c>.</summary>
public sealed record TestMember(Guid UserId, string Email, string Password);

/// <summary>
/// An organization with one signed-in member per role, each on a client of their own that already sends the
/// organization's slug. The Admin is the founder, from signup. Disposing it disposes the clients.
/// </summary>
public sealed record TestOrganization(
    Guid Id,
    string Slug,
    HttpClient Admin,
    HttpClient Manager,
    HttpClient Technician,
    HttpClient Tenant) : IDisposable
{
    /// <summary>The client of the member with <paramref name="role"/>, for theories over roles.</summary>
    public HttpClient As(Role role) => role switch
    {
        Role.Admin => Admin,
        Role.Manager => Manager,
        Role.Technician => Technician,
        Role.Tenant => Tenant,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "No member has this role."),
    };

    public void Dispose()
    {
        Admin.Dispose();
        Manager.Dispose();
        Technician.Dispose();
        Tenant.Dispose();
    }
}

/// <summary>Two organizations for cross-tenant tests (<see cref="CrossTenantExtensions.TwoOrganizationsAsync"/>).</summary>
public sealed record CrossTenant(TestOrganization A, TestOrganization B) : IDisposable
{
    public void Dispose()
    {
        A.Dispose();
        B.Dispose();
    }
}
