using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FixFlow.API.Auth;
using FixFlow.API.IntegrationTests.Fixtures;
using FixFlow.Domain.Organizations;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Serilog.Events;

namespace FixFlow.API.IntegrationTests.Organizations;

/// <summary>
/// The organization context middleware, through <see cref="OrganizationContextTestEndpoints"/>: what a request that names
/// the caller's organization gets, how the others are refused, and that organization-less endpoints ignore the header.
/// </summary>
public class OrganizationContextTests(FixFlowApiFactory api)
{
    private static readonly Uri _contextPath = new($"{OrganizationContextTestEndpoints.Path}/context", UriKind.Relative);

    private static readonly Uri _membershipsPath =
        new($"{OrganizationContextTestEndpoints.Path}/memberships", UriKind.Relative);

    private static readonly Uri _trustedScopePath =
        new($"{OrganizationContextTestEndpoints.Path}/trusted-scope", UriKind.Relative);

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    /// <summary>What an organization-less endpoint is sent in the organization header.</summary>
    public enum HeaderSent
    {
        None,
        ForeignSlug,
        UnknownSlug,
        Repeated,
    }

    [Fact]
    public async Task Context_OwnSlug_HasTheOrganizationAndTheCallersRoles()
    {
        using var client = api.CreateApiClient();
        var signedUp = await client.SignUpAsync();
        client.WithOrganization(signedUp.Slug);

        var context = await ReadContextAsync(client);

        Assert.Equal(signedUp.OrganizationId, context.OrganizationId);
        Assert.Equal(["Admin"], context.Roles);
    }

    [Theory]
    [MemberData(nameof(TestData.EveryRole), MemberType = typeof(TestData))]
    public async Task Context_EachRole_HasExactlyThatRole(Role role)
    {
        using var organizations = await api.TwoOrganizationsAsync();

        var context = await ReadContextAsync(organizations.A.As(role));

        Assert.Equal(organizations.A.Id, context.OrganizationId);
        Assert.Equal([role.ToString()], context.Roles);
    }

    [Fact]
    public async Task Context_MemberOfBoth_HasTheRolesOfTheOrganizationItNames()
    {
        using var organizations = await api.TwoOrganizationsAsync();
        var member = await api.MemberOfBothAsync(
            organizations.A.Id, [Role.Manager], organizations.B.Id, [Role.Technician, Role.Tenant]);
        using var client = await api.SignInAsync(member);

        var inA = await ReadContextAsync(client.WithOrganization(organizations.A.Slug));
        var inB = await ReadContextAsync(client.WithOrganization(organizations.B.Slug));

        Assert.Equal(organizations.A.Id, inA.OrganizationId);
        Assert.Equal(["Manager"], inA.Roles);
        Assert.Equal(organizations.B.Id, inB.OrganizationId);
        Assert.Equal(["Tenant", "Technician"], inB.Roles);
    }

    [Fact]
    public async Task Context_OtherOrganizationsSlug_Returns404ForEveryRole()
    {
        using var organizations = await api.TwoOrganizationsAsync();

        foreach (var (member, other) in new[] { (organizations.A, organizations.B), (organizations.B, organizations.A) })
        {
            foreach (var role in Enum.GetValues<Role>())
            {
                // The member's own client sends their own slug; this request names the other organization instead
                using var request = new HttpRequestMessage(HttpMethod.Get, _contextPath).WithOrganization(other.Slug);
                using var response = await member.As(role).SendAsync(request, _ct);

                await AssertBareNotFoundAsync(response);
            }
        }
    }

    [Fact]
    public async Task Context_UnknownSlug_Returns404()
    {
        using var client = api.CreateApiClient();
        await client.SignUpAsync();
        client.WithOrganization(TestData.OrganizationSlug());

        using var response = await client.GetAsync(_contextPath, _ct);

        await AssertBareNotFoundAsync(response);
    }

    [Theory]
    [InlineData("Acme")] // upper case
    [InlineData("api")] // reserved
    public async Task Context_InvalidSlug_Returns404(string slug)
    {
        using var client = api.CreateApiClient();
        await client.SignUpAsync();
        client.WithOrganization(slug);

        using var response = await client.GetAsync(_contextPath, _ct);

        await AssertBareNotFoundAsync(response);
    }

    [Fact]
    public async Task Context_OwnSlugInUpperCase_Returns404()
    {
        using var client = api.CreateApiClient();
        var signedUp = await client.SignUpAsync();
        // Slugs are taken as written (plan P21)
        client.WithOrganization(signedUp.Slug.ToUpperInvariant());

        using var response = await client.GetAsync(_contextPath, _ct);

        await AssertBareNotFoundAsync(response);
    }

    [Fact]
    public async Task Context_TwoSlugsInOneHeaderLine_Returns404()
    {
        using var client = api.CreateApiClient();
        var signedUp = await client.SignUpAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, _contextPath);
        // One value that holds a comma, not a repeated header: it's an invalid slug
        Assert.True(request.Headers.TryAddWithoutValidation(
            HttpClientExtensions.OrganizationHeader, $"{signedUp.Slug},{signedUp.Slug}"));

        using var response = await client.SendAsync(request, _ct);

        await AssertBareNotFoundAsync(response);
    }

    [Fact]
    public async Task Context_SuspendedMembership_Returns404()
    {
        var organization = await api.SignUpElsewhereAsync();
        var member = await api.AddMemberAsync(organization.OrganizationId, Role.Manager);
        await api.ChangeMembershipAsync(organization.OrganizationId, member.UserId, membership => membership.Suspend());
        using var client = (await api.SignInAsync(member)).WithOrganization(organization.Slug);

        using var response = await client.GetAsync(_contextPath, _ct);

        await AssertBareNotFoundAsync(response);
    }

    [Fact]
    public async Task Context_NoHeader_Returns400()
    {
        using var client = api.CreateApiClient();
        await client.SignUpAsync();

        using var response = await client.GetAsync(_contextPath, _ct);

        await AssertBadHeaderAsync(response);
    }

    [Fact]
    public async Task Context_EmptyHeader_Returns400()
    {
        // HttpClient drops an empty value, so the request is built on the server's side, with the session's cookie
        using var client = api.CreateApiClient();
        using var signUp = await client.PostAsJsonAsync(IdentityClientExtensions.SignUpPath, TestData.SignUp(), _ct);
        var cookie = AuthCookie.Find(signUp);
        Assert.NotNull(cookie);

        var context = await api.Server.SendAsync(
            http =>
            {
                http.Request.Method = HttpMethods.Get;
                http.Request.Scheme = Uri.UriSchemeHttps;
                http.Request.Path = _contextPath.OriginalString;
                http.Request.Headers.Cookie = new CookieHeaderValue(cookie.Name, cookie.Value).ToString();
                http.Request.Headers[HttpClientExtensions.OrganizationHeader] = "";
            },
            _ct);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        using var problem = await JsonDocument.ParseAsync(context.Response.Body, cancellationToken: _ct);
        Assert.Contains(
            HttpClientExtensions.OrganizationHeader,
            problem.RootElement.GetProperty("detail").GetString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Context_WhitespaceHeader_Returns400()
    {
        using var client = api.CreateApiClient();
        await client.SignUpAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, _contextPath);
        Assert.True(request.Headers.TryAddWithoutValidation(HttpClientExtensions.OrganizationHeader, "   "));

        using var response = await client.SendAsync(request, _ct);

        await AssertBadHeaderAsync(response);
    }

    [Fact]
    public async Task Context_RepeatedHeader_Returns400()
    {
        using var client = api.CreateApiClient();
        var signedUp = await client.SignUpAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, _contextPath);
        // Even the caller's own slug twice: the request must name one organization
        request.Headers.Add(HttpClientExtensions.OrganizationHeader, [signedUp.Slug, signedUp.Slug]);

        using var response = await client.SendAsync(request, _ct);

        await AssertBadHeaderAsync(response);
    }

    [Fact]
    public async Task Context_AnonymousWithUnknownSlug_Returns401()
    {
        using var client = api.CreateApiClient();
        client.WithOrganization(TestData.OrganizationSlug());

        using var response = await client.GetAsync(_contextPath, _ct);

        // Not a 404: an anonymous caller can't probe for slugs
        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Context_AnonymousWithExistingSlug_Returns401()
    {
        var organization = await api.SignUpElsewhereAsync();
        using var client = api.CreateApiClient();
        client.WithOrganization(organization.Slug);

        using var response = await client.GetAsync(_contextPath, _ct);

        // The same answer as for an unknown slug
        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Context_NextRequestAfterSuspension_Returns404OnlyThere()
    {
        using var organizations = await api.TwoOrganizationsAsync();
        var member = await api.MemberOfBothAsync(organizations.A.Id, [Role.Manager], organizations.B.Id, [Role.Manager]);
        using var client = await api.SignInAsync(member);
        // Caches both memberships
        await ReadContextAsync(client.WithOrganization(organizations.A.Slug));
        await ReadContextAsync(client.WithOrganization(organizations.B.Slug));

        await api.ChangeMembershipAsync(organizations.A.Id, member.UserId, membership => membership.Suspend());

        using var inA = await client.WithOrganization(organizations.A.Slug).GetAsync(_contextPath, _ct);
        await AssertBareNotFoundAsync(inA);
        // A suspension revokes one organization, not the others (§8)
        Assert.Equal(organizations.B.Id, (await ReadContextAsync(client.WithOrganization(organizations.B.Slug))).OrganizationId);
    }

    [Fact]
    public async Task Context_NextRequestAfterReactivation_HasTheOrganization()
    {
        var organization = await api.SignUpElsewhereAsync();
        var member = await api.AddMemberAsync(organization.OrganizationId, Role.Technician);
        using var client = (await api.SignInAsync(member)).WithOrganization(organization.Slug);
        await api.ChangeMembershipAsync(organization.OrganizationId, member.UserId, membership => membership.Suspend());
        // Caches the miss
        using (var suspended = await client.GetAsync(_contextPath, _ct))
        {
            await AssertBareNotFoundAsync(suspended);
        }

        await api.ChangeMembershipAsync(organization.OrganizationId, member.UserId, membership => membership.Reactivate());

        var context = await ReadContextAsync(client);
        Assert.Equal(organization.OrganizationId, context.OrganizationId);
        Assert.Equal(["Technician"], context.Roles);
    }

    [Fact]
    public async Task Context_NextRequestAfterRoleChange_HasTheNewRoles()
    {
        var organization = await api.SignUpElsewhereAsync();
        var member = await api.AddMemberAsync(organization.OrganizationId, Role.Manager);
        using var client = (await api.SignInAsync(member)).WithOrganization(organization.Slug);
        Assert.Equal(["Manager"], (await ReadContextAsync(client)).Roles);

        await api.ChangeMembershipAsync(
            organization.OrganizationId, member.UserId, membership => membership.ChangeRoles([Role.Manager, Role.Technician]));

        Assert.Equal(["Technician", "Manager"], (await ReadContextAsync(client)).Roles);
    }

    [Fact]
    public async Task Memberships_OrganizationA_SeesOnlyItsMembers()
    {
        using var organizations = await api.TwoOrganizationsAsync();
        var membersOfA = await UserIdsAsync(organizations.A);

        var userIds = await organizations.A.Admin.GetFromJsonAsync<Guid[]>(_membershipsPath, _ct);

        // B and other tests' organizations share the database: the filter keeps them out
        Assert.NotNull(userIds);
        Assert.Equal(membersOfA.Order(), userIds.Order());
    }

    [Fact]
    public async Task TrustedScope_InOrganizationRequest_Returns500FromTheScopesGuard()
    {
        using var client = api.CreateApiClient();
        var signedUp = await client.SignUpAsync();
        var userId = (await client.GetMeAsync()).Id.ToString();
        client.WithOrganization(signedUp.Slug);

        using var response = await client.PostAsync(_trustedScopePath, content: null, _ct);

        await response.ReadProblemAsync(HttpStatusCode.InternalServerError);
        // Not some other failure: the scope refused to replace the organization the request resolved (plan P25). The
        // exception handler's event has the exception but not the user, whose log context has ended, so it is found
        // through the trace of the request's completion event.
        var traceId = Assert.Single(EventsOf(userId), logEvent => IsCompletionOf(logEvent, _trustedScopePath)).TraceId;
        Assert.NotNull(traceId);
        Assert.Contains(api.Logs.Events, logEvent => logEvent.TraceId == traceId
            && logEvent.Exception is InvalidOperationException { Message: "The request already has an organization context." });
    }

    [Fact]
    public async Task Context_OrganizationRequest_SetsNoAuthCookie()
    {
        using var client = api.CreateApiClient();
        var signedUp = await client.SignUpAsync();
        client.WithOrganization(signedUp.Slug);

        using var response = await client.GetAsync(_contextPath, _ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(AuthCookie.Find(response));
    }

    [Fact]
    public async Task Context_CookieRenewedInOrganizationRequest_CarriesNoOrganization()
    {
        await using var app = api.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.PostConfigure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options =>
            {
                var validate = options.Events.OnValidatePrincipal;
                options.Events.OnValidatePrincipal = async context =>
                {
                    await validate(context);
                    // As sliding expiration does once half of the cookie's lifetime has passed
                    context.ShouldRenew = true;
                };
            })));
        using var client = app.CreateApiClient();
        var signedUp = await client.SignUpAsync();
        client.WithOrganization(signedUp.Slug);

        using var response = await client.GetAsync(_contextPath, _ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = AuthCookie.Find(response);
        Assert.NotNull(cookie);
        var ticket = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme)
            .TicketDataFormat.Unprotect(cookie.Value.Value);
        Assert.NotNull(ticket);
        // The renewed cookie still has the session, and only the session: the organization is named per request (§8)
        var identity = Assert.Single(ticket.Principal.Identities);
        Assert.Equal(IdentityConstants.ApplicationScheme, identity.AuthenticationType);
        Assert.Null(ticket.Principal.FindFirst(FixFlowClaimTypes.OrganizationId));
        Assert.Null(ticket.Principal.FindFirst(FixFlowClaimTypes.Role));
    }

    [Fact]
    public async Task Context_LogEvents_CarryTheOrganizationId()
    {
        using var client = api.CreateApiClient();
        var signedUp = await client.SignUpAsync();
        client.WithOrganization(signedUp.Slug);
        var userId = (await client.GetMeAsync()).Id.ToString();

        await ReadContextAsync(client);

        // By the user id, which every event of a signed-in request carries
        var events = EventsOf(userId).ToList();
        var read = Assert.Single(
            events, logEvent => logEvent.MessageTemplate.Text == OrganizationContextTestEndpoints.ContextReadMessage);
        var completion = Assert.Single(events, logEvent => IsCompletionOf(logEvent, _contextPath));
        Assert.Equal(signedUp.OrganizationId.ToString(), Property(read, "OrganizationId"));
        Assert.Equal(signedUp.OrganizationId.ToString(), Property(completion, "OrganizationId"));
        // /me is organization-less: the same header gave it no organization
        var me = Assert.Single(events, logEvent => IsCompletionOf(logEvent, IdentityClientExtensions.MePath));
        Assert.Null(Property(me, "OrganizationId"));
    }

    [Fact]
    public async Task Refusals_DebugLog_NamesOnlyAValidSlug()
    {
        // The header's value is the client's, and anything could be pasted into it: only a slug may reach the log (S26)
        await using var app = api.WithWebHostBuilder(builder => builder.UseSetting(
            "Serilog:MinimumLevel:Override:FixFlow.API.Organizations", "Debug"));
        using var client = app.CreateApiClient();
        await client.SignUpAsync();
        var userId = (await client.GetMeAsync()).Id.ToString();
        var pasted = TestData.Name("Pasted");
        var unknown = TestData.OrganizationSlug();

        foreach (var value in new[] { pasted, unknown })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _contextPath).WithOrganization(value);
            using var response = await client.SendAsync(request, _ct);
            await AssertBareNotFoundAsync(response);
        }

        // Both refusals are logged, so Debug is on
        Assert.Equal(2, EventsOf(userId).Count(logEvent => logEvent.Level == LogEventLevel.Debug));
        Assert.Empty(api.Logs.Mentioning(pasted));
        Assert.Single(api.Logs.Mentioning(unknown));
    }

    [Theory]
    [InlineData(HeaderSent.None)]
    [InlineData(HeaderSent.ForeignSlug)]
    [InlineData(HeaderSent.UnknownSlug)]
    [InlineData(HeaderSent.Repeated)]
    public async Task OrganizationLessEndpoints_AnyHeader_IgnoreIt(HeaderSent header)
    {
        using var client = api.CreateApiClient();
        var signedUp = await client.SignUpAsync();
        switch (header)
        {
            case HeaderSent.ForeignSlug:
                client.WithOrganization((await api.SignUpElsewhereAsync()).Slug);
                break;
            case HeaderSent.UnknownSlug:
                client.WithOrganization(TestData.OrganizationSlug());
                break;
            case HeaderSent.Repeated:
                client.DefaultRequestHeaders.Add(HttpClientExtensions.OrganizationHeader, [signedUp.Slug, signedUp.Slug]);
                break;
        }

        // Each asserts its success; logout comes last, since it ends the session
        Assert.Equal([signedUp.Slug], (await client.GetMeAsync()).Memberships.Select(m => m.Slug));
        using (var renamed = await client.ChangeNameAsync("Renamed", "Member"))
        {
            Assert.Equal(HttpStatusCode.NoContent, renamed.StatusCode);
        }

        await client.CreateOrganizationAsync(TestData.Name("Org"), TestData.OrganizationSlug());
        using var loggedOut = await client.PostAsync(IdentityClientExtensions.LogOutPath, content: null, _ct);
        Assert.Equal(HttpStatusCode.NoContent, loggedOut.StatusCode);
    }

    private async Task<ContextRead> ReadContextAsync(HttpClient client)
    {
        using var response = await client.GetAsync(_contextPath, _ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<ContextRead>(_ct)
            ?? throw new InvalidOperationException("The response has no body.");
    }

    /// <summary>The ids of the organization's members, one per role.</summary>
    private static async Task<Guid[]> UserIdsAsync(TestOrganization organization) =>
        await Task.WhenAll(Enum.GetValues<Role>().Select(async role => (await organization.As(role).GetMeAsync()).Id));

    /// <summary>The bare 404 of every miss: the same Problem Details as any other, with no detail to tell which.</summary>
    private static async Task AssertBareNotFoundAsync(HttpResponseMessage response)
    {
        var problem = await response.ReadProblemAsync(HttpStatusCode.NotFound);
        Assert.False(problem.TryGetProperty("detail", out _));
    }

    /// <summary>A client bug: the detail names the header.</summary>
    private static async Task AssertBadHeaderAsync(HttpResponseMessage response)
    {
        var problem = await response.ReadProblemAsync(HttpStatusCode.BadRequest);
        Assert.Contains(
            HttpClientExtensions.OrganizationHeader, problem.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    private IEnumerable<LogEvent> EventsOf(string userId) =>
        api.Logs.Events.Where(logEvent => Property(logEvent, "UserId") == userId);

    private static bool IsCompletionOf(LogEvent logEvent, Uri path) =>
        logEvent.MessageTemplate.Text.StartsWith("HTTP ", StringComparison.Ordinal)
        && Property(logEvent, "RequestPath") == path.OriginalString;

    private static string? Property(LogEvent logEvent, string name) =>
        logEvent.Properties.TryGetValue(name, out var value) && value is ScalarValue { Value: { } scalar }
            ? scalar.ToString()
            : null;

    /// <param name="Roles">As the JSON names them.</param>
    private sealed record ContextRead(Guid? OrganizationId, string[] Roles);
}
