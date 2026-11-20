using System.Net;
using System.Net.Http.Json;
using FixFlow.API.IntegrationTests.Fixtures;
using FixFlow.Application.Persistence;
using FixFlow.Domain.Organizations;
using FixFlow.Domain.Properties;
using Microsoft.EntityFrameworkCore;
using static FixFlow.API.IntegrationTests.Fixtures.PropertyClientExtensions;

namespace FixFlow.API.IntegrationTests.Properties;

public class PropertyEndpointsTests(FixFlowApiFactory api)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(Role.Admin)]
    [InlineData(Role.Manager)]
    public async Task CreateProperty_AsAdminOrManager_Returns201WithLocationAndTheTrimmedProperty(Role role)
    {
        using var organization = await api.OrganizationWithEveryRoleAsync();
        var request = TestData.Property();

        using var response = await organization.As(role).PostAsJsonAsync(
            PropertiesPath, request with { Name = $"  {request.Name} " }, _ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<PropertyResponse>(_ct);
        Assert.Equal(7, created!.Id.Version);
        Assert.Equal(new PropertyResponse(created.Id, request.Name, request.Address!, "Europe/Warsaw"), created);
        Assert.Equal(PropertyPath(created.Id).ToString(), response.Headers.Location!.AbsolutePath);
        Assert.Equal(created, await organization.As(role).GetPropertyAsync(created.Id));
    }

    [Fact]
    public async Task GetProperty_UnknownId_Returns404()
    {
        using var client = await AdminAsync();

        using var response = await client.GetAsync(PropertyPath(Guid.CreateVersion7()), _ct);

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListProperties_Pages_OrderedByNameWithTotalCount()
    {
        using var client = await AdminAsync();
        foreach (var name in new[] { "Beta Court", "Alpha House", "Gamma Lofts" })
        {
            await client.CreatePropertyAsync(TestData.Property() with { Name = name });
        }

        var first = await client.ListPropertiesAsync("page=1&pageSize=2");
        var second = await client.ListPropertiesAsync("page=2&pageSize=2");

        Assert.Equal(["Alpha House", "Beta Court"], first.Items.Select(p => p.Name));
        Assert.Equal((1, 2, 3), (first.Page, first.PageSize, first.TotalCount));
        Assert.Equal(["Gamma Lofts"], second.Items.Select(p => p.Name));
    }

    [Theory]
    [InlineData("RIVER", new[] { "Old Riverbank", "Riverside Apartments" })]
    [InlineData("  view ", new[] { "Harbour View" })]
    // A plain substring: LIKE's wildcards match only themselves
    [InlineData("%", new[] { "Lofts 100%" })]
    [InlineData("_", new string[0])]
    [InlineData("", new[] { "Harbour View", "Lofts 100%", "Old Riverbank", "Riverside Apartments" })]
    public async Task ListProperties_NameFilter_MatchesASubstringIgnoringCase(string filter, string[] expected)
    {
        using var client = await AdminAsync();
        foreach (var name in new[] { "Riverside Apartments", "Harbour View", "Old Riverbank", "Lofts 100%" })
        {
            await client.CreatePropertyAsync(TestData.Property() with { Name = name });
        }

        var page = await client.ListPropertiesAsync($"name={Uri.EscapeDataString(filter)}");

        Assert.Equal(expected, page.Items.Select(p => p.Name));
        Assert.Equal(expected.Length, page.TotalCount);
    }

    [Fact]
    public async Task ListProperties_NameFilterTooLong_Returns400OnName()
    {
        using var client = await AdminAsync();

        using var response = await client.GetAsync(
            new Uri($"{PropertiesPath}?name={new string('n', Property.NameMaxLength + 1)}", UriKind.Relative), _ct);

        var problem = await response.ReadProblemAsync(HttpStatusCode.BadRequest);
        Assert.Equal(["name"], problem.FieldErrors().Keys);
    }

    [Fact]
    public async Task UpdateProperty_ValidRequest_Returns204AndReplacesEveryField()
    {
        using var client = await AdminAsync();
        var created = await client.CreatePropertyAsync();
        var update = new PropertyRequest(
            TestData.Name("Renamed"),
            new PropertyAddress("2 Other Road", "Building C", "Otherton", null, "NO"),
            "Europe/Oslo");

        using var response = await client.PutAsJsonAsync(PropertyPath(created.Id), update, _ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        // Stored as written: Europe/Oslo isn't canonicalized to Europe/Berlin
        Assert.Equal(
            new PropertyResponse(created.Id, update.Name, update.Address!, "Europe/Oslo"),
            await client.GetPropertyAsync(created.Id));
    }

    [Theory]
    [InlineData("name")]
    [InlineData("address")]
    [InlineData("address.line1")]
    [InlineData("address.city")]
    [InlineData("address.postalCode")]
    [InlineData("address.countryCode")]
    [InlineData("timeZone")]
    public async Task CreateAndUpdateProperty_InvalidField_Return400OnThatField(string field)
    {
        using var client = await AdminAsync();
        var existing = await client.CreatePropertyAsync();
        var valid = TestData.Property();
        var address = valid.Address!;
        var invalid = field switch
        {
            "name" => valid with { Name = "Riverside\nApartments" },
            "address" => valid with { Address = null },
            "address.line1" => valid with { Address = address with { Line1 = "  " } },
            "address.city" => valid with { Address = address with { City = new string('c', Address.CityMaxLength + 1) } },
            "address.postalCode" =>
                valid with { Address = address with { PostalCode = new string('p', Address.PostalCodeMaxLength + 1) } },
            "address.countryCode" => valid with { Address = address with { CountryCode = "pl" } },
            "timeZone" => valid with { TimeZone = "Central European Standard Time" },
            _ => throw new ArgumentOutOfRangeException(nameof(field)),
        };

        using var created = await client.PostAsJsonAsync(PropertiesPath, invalid, _ct);
        using var updated = await client.PutAsJsonAsync(PropertyPath(existing.Id), invalid, _ct);

        Assert.Equal([field], (await created.ReadProblemAsync(HttpStatusCode.BadRequest)).FieldErrors().Keys);
        Assert.Equal([field], (await updated.ReadProblemAsync(HttpStatusCode.BadRequest)).FieldErrors().Keys);
        Assert.Equal(existing, await client.GetPropertyAsync(existing.Id));
    }

    [Theory]
    [InlineData("UTC+02:00")]
    [InlineData("Etc/GMT-2")]
    [InlineData("UTC")]
    public async Task CreateProperty_FixedOffsetZone_Returns400WithTheZoneRule(string timeZone)
    {
        using var client = await AdminAsync();
        Assert.False(PropertyTimeZone.IsValid(timeZone, out var expected));

        using var response = await client.PostAsJsonAsync(PropertiesPath, TestData.Property() with { TimeZone = timeZone }, _ct);

        var problem = await response.ReadProblemAsync(HttpStatusCode.BadRequest);
        Assert.Equal([expected], problem.FieldErrors()["timeZone"]);
        Assert.Equal(0, (await client.ListPropertiesAsync()).TotalCount);
    }

    [Fact]
    public async Task ArchiveProperty_WithUnits_Returns204AndArchivesItAndItsUnitsOnly()
    {
        using var client = api.CreateApiClient();
        var signedUp = await client.SignUpAsync();
        client.WithOrganization(signedUp.Slug);
        var archived = await client.CreatePropertyAsync();
        var units = new[] { await client.CreateUnitAsync(archived.Id), await client.CreateUnitAsync(archived.Id) };
        var kept = await client.CreatePropertyAsync();
        var keptUnit = await client.CreateUnitAsync(kept.Id);

        using var response = await client.PostAsync(ArchivePropertyPath(archived.Id), null, _ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using (var get = await client.GetAsync(PropertyPath(archived.Id), _ct))
        {
            await get.ReadProblemAsync(HttpStatusCode.NotFound);
        }

        Assert.Equal([kept.Id], (await client.ListPropertiesAsync()).Items.Select(p => p.Id));
        Assert.Equal([keptUnit], (await client.ListUnitsAsync(kept.Id)).Items);
        // Every unit of the property was archived at the same instant as the property, and none of the other one's
        var deletedAt = await api.InOrganizationAsync(signedUp.OrganizationId, db => db.Units
            // Archived rows are what this checks: the soft-delete filter would hide them
            .IgnoreQueryFilters([QueryFilters.SoftDelete])
            .ToDictionaryAsync(u => u.Id, u => u.DeletedAt, _ct));
        Assert.All(units, unit => Assert.Equal(FixFlowApiFactory.SharedNow, deletedAt[unit.Id]));
        Assert.Null(deletedAt[keptUnit.Id]);
    }

    [Fact]
    public async Task ArchiveProperty_AlreadyArchived_Returns404()
    {
        using var client = await AdminAsync();
        var property = await client.CreatePropertyAsync();
        using (var first = await client.PostAsync(ArchivePropertyPath(property.Id), null, _ct))
        {
            Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        }

        using var second = await client.PostAsync(ArchivePropertyPath(property.Id), null, _ct);

        await second.ReadProblemAsync(HttpStatusCode.NotFound);
    }

    /// <summary>A new organization's founder, signed in and sending its slug.</summary>
    private async Task<HttpClient> AdminAsync()
    {
        var client = api.CreateApiClient();
        return client.WithOrganization((await client.SignUpAsync()).Slug);
    }
}
