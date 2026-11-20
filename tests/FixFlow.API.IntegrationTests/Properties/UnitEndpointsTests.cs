using System.Net;
using System.Net.Http.Json;
using FixFlow.API.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using static FixFlow.API.IntegrationTests.Fixtures.PropertyClientExtensions;

namespace FixFlow.API.IntegrationTests.Properties;

public class UnitEndpointsTests(FixFlowApiFactory api)
{
    private const string NameTaken = "Another unit in this property already has this name.";

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateUnit_ValidName_Returns201WithTheTrimmedUnitAndNoLocation()
    {
        using var client = await AdminAsync();
        var property = await client.CreatePropertyAsync();

        using var response = await client.PostAsJsonAsync(UnitsPath(property.Id), new UnitRequest(" 4B "), _ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        // Units have no GET endpoint of their own
        Assert.Null(response.Headers.Location);
        var created = await response.Content.ReadFromJsonAsync<UnitResponse>(_ct);
        Assert.Equal(7, created!.Id.Version);
        Assert.Equal(new UnitResponse(created.Id, property.Id, "4B"), created);
    }

    [Fact]
    public async Task ListUnits_Pages_OrderedByNameWithTotalCount()
    {
        using var client = await AdminAsync();
        var property = await client.CreatePropertyAsync();
        var other = await client.CreatePropertyAsync();
        foreach (var name in new[] { "3", "1", "2" })
        {
            await client.CreateUnitAsync(property.Id, name);
        }

        await client.CreateUnitAsync(other.Id, "0");

        var first = await client.ListUnitsAsync(property.Id, "page=1&pageSize=2");
        var second = await client.ListUnitsAsync(property.Id, "page=2&pageSize=2");

        Assert.Equal(["1", "2"], first.Items.Select(u => u.Name));
        Assert.Equal((1, 2, 3), (first.Page, first.PageSize, first.TotalCount));
        Assert.Equal(["3"], second.Items.Select(u => u.Name));
    }

    [Fact]
    public async Task CreateUnit_NameTakenInAnotherCase_Returns422OnName()
    {
        using var client = await AdminAsync();
        var property = await client.CreatePropertyAsync();
        await client.CreateUnitAsync(property.Id, "4B");

        using var response = await client.PostAsJsonAsync(UnitsPath(property.Id), new UnitRequest("4b"), _ct);

        var problem = await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity);
        Assert.Equal([NameTaken], problem.FieldErrors()["name"]);
        Assert.Equal(1, (await client.ListUnitsAsync(property.Id)).TotalCount);
    }

    [Fact]
    public async Task CreateUnit_NameOfAnArchivedUnit_Succeeds()
    {
        using var client = await AdminAsync();
        var property = await client.CreatePropertyAsync();
        var archived = await client.CreateUnitAsync(property.Id, "4B");
        using (var archive = await client.PostAsync(ArchiveUnitPath(property.Id, archived.Id), null, _ct))
        {
            Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);
        }

        var created = await client.CreateUnitAsync(property.Id, "4B");

        Assert.Equal([created], (await client.ListUnitsAsync(property.Id)).Items);
    }

    [Fact]
    public async Task RenameUnit_ValidName_Returns204AndRenamesIt()
    {
        using var client = await AdminAsync();
        var property = await client.CreatePropertyAsync();
        var unit = await client.CreateUnitAsync(property.Id, "4B");

        using var response = await client.PutAsJsonAsync(UnitPath(property.Id, unit.Id), new UnitRequest(" 4C "), _ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal([unit with { Name = "4C" }], (await client.ListUnitsAsync(property.Id)).Items);
    }

    [Fact]
    public async Task RenameUnit_ToAnotherUnitsNameInAnotherCase_Returns422OnName()
    {
        using var client = await AdminAsync();
        var property = await client.CreatePropertyAsync();
        await client.CreateUnitAsync(property.Id, "4B");
        var unit = await client.CreateUnitAsync(property.Id, "4C");

        using var response = await client.PutAsJsonAsync(UnitPath(property.Id, unit.Id), new UnitRequest("4b"), _ct);

        var problem = await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity);
        Assert.Equal([NameTaken], problem.FieldErrors()["name"]);
        Assert.Contains(unit, (await client.ListUnitsAsync(property.Id)).Items);
    }

    [Fact]
    public async Task ArchiveUnit_Returns204AndHidesItFromTheList()
    {
        using var client = await AdminAsync();
        var property = await client.CreatePropertyAsync();
        var archived = await client.CreateUnitAsync(property.Id);
        var kept = await client.CreateUnitAsync(property.Id);

        using var response = await client.PostAsync(ArchiveUnitPath(property.Id, archived.Id), null, _ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal([kept], (await client.ListUnitsAsync(property.Id)).Items);
        using var again = await client.PostAsync(ArchiveUnitPath(property.Id, archived.Id), null, _ct);
        await again.ReadProblemAsync(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("")]
    [InlineData("4\nB")]
    [InlineData("123456789012345678901234567890123456789012345678901")]
    public async Task CreateAndRenameUnit_InvalidName_Return400OnName(string name)
    {
        using var client = await AdminAsync();
        var property = await client.CreatePropertyAsync();
        var unit = await client.CreateUnitAsync(property.Id, "4B");

        using var created = await client.PostAsJsonAsync(UnitsPath(property.Id), new UnitRequest(name), _ct);
        using var renamed = await client.PutAsJsonAsync(UnitPath(property.Id, unit.Id), new UnitRequest(name), _ct);

        Assert.Equal(["name"], (await created.ReadProblemAsync(HttpStatusCode.BadRequest)).FieldErrors().Keys);
        Assert.Equal(["name"], (await renamed.ReadProblemAsync(HttpStatusCode.BadRequest)).FieldErrors().Keys);
        Assert.Equal([unit], (await client.ListUnitsAsync(property.Id)).Items);
    }

    [Fact]
    public async Task ListUnits_PageSizeOutOfRange_Returns400OnPageSize()
    {
        using var client = await AdminAsync();
        var property = await client.CreatePropertyAsync();

        using var response = await client.GetAsync(new Uri($"{UnitsPath(property.Id)}?pageSize=0", UriKind.Relative), _ct);

        Assert.Equal(["pageSize"], (await response.ReadProblemAsync(HttpStatusCode.BadRequest)).FieldErrors().Keys);
    }

    [Fact]
    public async Task UnitRoutes_UnitOfAnotherProperty_Return404AndChangeNothing()
    {
        using var client = await AdminAsync();
        var property = await client.CreatePropertyAsync();
        var unit = await client.CreateUnitAsync(property.Id, "4B");
        var other = await client.CreatePropertyAsync();

        using var renamed = await client.PutAsJsonAsync(UnitPath(other.Id, unit.Id), new UnitRequest("4C"), _ct);
        using var archived = await client.PostAsync(ArchiveUnitPath(other.Id, unit.Id), null, _ct);

        await renamed.ReadProblemAsync(HttpStatusCode.NotFound);
        await archived.ReadProblemAsync(HttpStatusCode.NotFound);
        Assert.Equal([unit], (await client.ListUnitsAsync(property.Id)).Items);
    }

    [Fact]
    public async Task UnitRoutes_ArchivedPropertyWithAnActiveUnit_Return404()
    {
        using var client = api.CreateApiClient();
        var signedUp = await client.SignUpAsync();
        client.WithOrganization(signedUp.Slug);
        var property = await client.CreatePropertyAsync();
        var unit = await client.CreateUnitAsync(property.Id);
        // The property alone, as a unit created during its archive would be left: the routes check the property too
        await api.InOrganizationAsync(signedUp.OrganizationId, async db =>
        {
            (await db.Properties.SingleAsync(p => p.Id == property.Id, _ct)).Archive(FixFlowApiFactory.SharedNow);
            return await db.SaveChangesAsync(_ct);
        });

        using var listed = await client.GetAsync(UnitsPath(property.Id), _ct);
        using var created = await client.PostAsJsonAsync(UnitsPath(property.Id), new UnitRequest(TestData.UnitName()), _ct);
        using var renamed = await client.PutAsJsonAsync(UnitPath(property.Id, unit.Id), new UnitRequest("4C"), _ct);
        using var archived = await client.PostAsync(ArchiveUnitPath(property.Id, unit.Id), null, _ct);

        foreach (var response in new[] { listed, created, renamed, archived })
        {
            await response.ReadProblemAsync(HttpStatusCode.NotFound);
        }

        var stored = await api.InOrganizationAsync(signedUp.OrganizationId, db => db.Units.SingleAsync(u => u.Id == unit.Id, _ct));
        Assert.Equal((unit.Name, (Instant?)null), (stored.Name, stored.DeletedAt));
    }

    /// <summary>A new organization's founder, signed in and sending its slug.</summary>
    private async Task<HttpClient> AdminAsync()
    {
        var client = api.CreateApiClient();
        return client.WithOrganization((await client.SignUpAsync()).Slug);
    }
}
