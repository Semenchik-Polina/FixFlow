using System.Net;
using System.Net.Http.Json;
using FixFlow.API.IntegrationTests.Fixtures;
using FixFlow.Domain.Organizations;
using static FixFlow.API.IntegrationTests.Fixtures.PropertyClientExtensions;

namespace FixFlow.API.IntegrationTests.Properties;

/// <summary>
/// Who may use the property and unit endpoints (§8), over all ten routes: Managers and Admins only (403 for the
/// other roles), and in their own organization only (another organization's ids are 404s that change nothing). Every
/// request has a valid body, so validation can't answer first.
/// </summary>
public class PropertyAccessTests(FixFlowApiFactory api)
{
    private static readonly string[] _routes =
    [
        "ListProperties", "ListPropertyTimeZones", "GetProperty", "CreateProperty", "UpdateProperty", "ArchiveProperty",
        "ListUnits", "CreateUnit", "RenameUnit", "ArchiveUnit",
    ];

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(Role.Technician)]
    [InlineData(Role.Tenant)]
    public async Task EveryRoute_WithoutManagerOrAdmin_Returns403(Role role)
    {
        using var organization = await api.OrganizationWithEveryRoleAsync();
        var property = await organization.Admin.CreatePropertyAsync();
        var unit = await organization.Admin.CreateUnitAsync(property.Id);

        var statuses = await StatusesAsync(organization.As(role), _routes, property.Id, unit.Id);

        Assert.Equal(_routes.ToDictionary(route => route, _ => HttpStatusCode.Forbidden), statuses);
        Assert.Equal([unit], (await organization.Admin.ListUnitsAsync(property.Id)).Items);
        Assert.Equal(property, await organization.Admin.GetPropertyAsync(property.Id));
    }

    [Theory]
    [InlineData(Role.Admin)]
    [InlineData(Role.Manager)]
    public async Task EveryRouteWithIds_AnotherOrganizationsIds_Returns404AndChangesNothing(Role role)
    {
        using var orgs = await api.TwoOrganizationsAsync();
        var property = await orgs.B.Admin.CreatePropertyAsync();
        var unit = await orgs.B.Admin.CreateUnitAsync(property.Id);
        string[] withIds = [.. _routes.Except(["ListProperties", "ListPropertyTimeZones", "CreateProperty"])];

        var statuses = await StatusesAsync(orgs.A.As(role), withIds, property.Id, unit.Id);

        Assert.Equal(withIds.ToDictionary(route => route, _ => HttpStatusCode.NotFound), statuses);
        Assert.Equal(property, await orgs.B.Admin.GetPropertyAsync(property.Id));
        Assert.Equal([unit], (await orgs.B.Admin.ListUnitsAsync(property.Id)).Items);
    }

    [Fact]
    public async Task ListProperties_ShowsOnlyTheCallersOrganization()
    {
        using var orgs = await api.TwoOrganizationsAsync();
        var request = TestData.Property();
        var own = await orgs.A.Admin.CreatePropertyAsync(request);
        await orgs.B.Admin.CreatePropertyAsync(request);

        var page = await orgs.A.Manager.ListPropertiesAsync($"name={Uri.EscapeDataString(request.Name)}");

        Assert.Equal([own], page.Items);
        Assert.Equal([own], (await orgs.A.Admin.ListPropertiesAsync()).Items);
    }

    [Fact]
    public async Task GetProperty_AnotherOrganizationsSlug_Returns404()
    {
        using var orgs = await api.TwoOrganizationsAsync();
        var property = await orgs.A.Admin.CreatePropertyAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, PropertyPath(property.Id)).WithOrganization(orgs.B.Slug);

        using var response = await orgs.A.Admin.SendAsync(request, _ct);

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListPropertyTimeZones_AnotherOrganizationsSlug_Returns404()
    {
        using var orgs = await api.TwoOrganizationsAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, TimeZonesPath).WithOrganization(orgs.B.Slug);

        using var response = await orgs.A.Manager.SendAsync(request, _ct);

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
    }

    /// <summary>Sends each route, with these ids and a valid body, and returns its status.</summary>
    private async Task<Dictionary<string, HttpStatusCode>> StatusesAsync(
        HttpClient client, IEnumerable<string> routes, Guid propertyId, Guid unitId)
    {
        var statuses = new Dictionary<string, HttpStatusCode>();
        foreach (var route in routes)
        {
            using var request = Request(route, propertyId, unitId);
            using var response = await client.SendAsync(request, _ct);
            statuses[route] = response.StatusCode;
        }

        return statuses;
    }

    private static HttpRequestMessage Request(string route, Guid propertyId, Guid unitId) => route switch
    {
        "ListProperties" => new(HttpMethod.Get, PropertiesPath),
        "ListPropertyTimeZones" => new(HttpMethod.Get, TimeZonesPath),
        "GetProperty" => new(HttpMethod.Get, PropertyPath(propertyId)),
        "CreateProperty" => WithBody(HttpMethod.Post, PropertiesPath, TestData.Property()),
        "UpdateProperty" => WithBody(HttpMethod.Put, PropertyPath(propertyId), TestData.Property()),
        "ArchiveProperty" => new(HttpMethod.Post, ArchivePropertyPath(propertyId)),
        "ListUnits" => new(HttpMethod.Get, UnitsPath(propertyId)),
        "CreateUnit" => WithBody(HttpMethod.Post, UnitsPath(propertyId), new UnitRequest(TestData.UnitName())),
        "RenameUnit" => WithBody(HttpMethod.Put, UnitPath(propertyId, unitId), new UnitRequest(TestData.UnitName())),
        "ArchiveUnit" => new(HttpMethod.Post, ArchiveUnitPath(propertyId, unitId)),
        _ => throw new ArgumentOutOfRangeException(nameof(route), route, "Not a property or unit route."),
    };

    private static HttpRequestMessage WithBody<T>(HttpMethod method, Uri path, T body) =>
        new(method, path) { Content = JsonContent.Create(body) };
}
