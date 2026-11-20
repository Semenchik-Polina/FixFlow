using System.Net;
using System.Net.Http.Json;
using FixFlow.API.Endpoints;

namespace FixFlow.API.IntegrationTests.Fixtures;

/// <summary>The property and unit endpoints, for a client that sends its organization's slug.</summary>
public static class PropertyClientExtensions
{
    public static readonly Uri PropertiesPath = new($"{EndpointModuleExtensions.ApiRoot}/properties", UriKind.Relative);

    public static Uri PropertyPath(Guid id) => new($"{PropertiesPath}/{id}", UriKind.Relative);

    public static Uri ArchivePropertyPath(Guid id) => new($"{PropertiesPath}/{id}/archive", UriKind.Relative);

    public static Uri UnitsPath(Guid propertyId) => new($"{PropertiesPath}/{propertyId}/units", UriKind.Relative);

    public static Uri UnitPath(Guid propertyId, Guid unitId) =>
        new($"{PropertiesPath}/{propertyId}/units/{unitId}", UriKind.Relative);

    public static Uri ArchiveUnitPath(Guid propertyId, Guid unitId) =>
        new($"{PropertiesPath}/{propertyId}/units/{unitId}/archive", UriKind.Relative);

    /// <summary>Creates <paramref name="request"/> (<see cref="TestData.Property"/> by default), which must succeed.</summary>
    public static async Task<PropertyResponse> CreatePropertyAsync(this HttpClient client, PropertyRequest? request = null)
    {
        using var response = await client.PostAsJsonAsync(
            PropertiesPath, request ?? TestData.Property(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<PropertyResponse>(response);
    }

    /// <summary>Creates a unit named <paramref name="name"/> (<see cref="TestData.UnitName"/> by default), which must succeed.</summary>
    public static async Task<UnitResponse> CreateUnitAsync(this HttpClient client, Guid propertyId, string? name = null)
    {
        using var response = await client.PostAsJsonAsync(
            UnitsPath(propertyId), new UnitRequest(name ?? TestData.UnitName()), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<UnitResponse>(response);
    }

    /// <summary><c>GET /properties/{id}</c>, which must succeed.</summary>
    public static Task<PropertyResponse> GetPropertyAsync(this HttpClient client, Guid id) =>
        GetAsync<PropertyResponse>(client, PropertyPath(id));

    /// <summary><c>GET /properties?{query}</c>, which must succeed.</summary>
    public static Task<ListPage<PropertyResponse>> ListPropertiesAsync(this HttpClient client, string query = "") =>
        GetAsync<ListPage<PropertyResponse>>(client, new Uri($"{PropertiesPath}?{query}", UriKind.Relative));

    /// <summary><c>GET /properties/{propertyId}/units?{query}</c>, which must succeed.</summary>
    public static Task<ListPage<UnitResponse>> ListUnitsAsync(this HttpClient client, Guid propertyId, string query = "") =>
        GetAsync<ListPage<UnitResponse>>(client, new Uri($"{UnitsPath(propertyId)}?{query}", UriKind.Relative));

    private static async Task<T> GetAsync<T>(HttpClient client, Uri path)
    {
        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync<T>(response);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<T>(TestContext.Current.CancellationToken)
        ?? throw new InvalidOperationException("The response has no body.");
}
