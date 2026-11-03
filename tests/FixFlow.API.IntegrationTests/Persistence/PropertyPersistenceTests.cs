using FixFlow.API.IntegrationTests.Fixtures;
using FixFlow.API.IntegrationTests.Persistence.TestModel;
using FixFlow.Application.Persistence;
using FixFlow.Domain.Properties;
using Microsoft.EntityFrameworkCore;

namespace FixFlow.API.IntegrationTests.Persistence;

public class PropertyPersistenceTests(FixFlowApiFactory api)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Property_Saved_RoundTripsWithAddressZoneAndOrganization()
    {
        var owner = await InNewOrganizationAsync();
        var address = Address.Create("Main Street 1", "Flat 2", "Springfield", "00-001", "PL");
        var property = await SavePropertyAsync(owner, address);

        await using var reader = api.Persistence.CreateScope(owner);
        var saved = await reader.Db.Properties.SingleAsync(p => p.Id == property.Id, _ct);
        Assert.Equal(property.Name, saved.Name);
        Assert.Equal(address, saved.Address);
        Assert.Equal("Europe/Warsaw", saved.TimeZoneId);
        Assert.Equal(owner.OrganizationId, saved.OrganizationId);
    }

    [Fact]
    public async Task Property_AddressWithoutOptionalParts_RoundTripsThemAsNull()
    {
        var owner = await InNewOrganizationAsync();
        var property = await SavePropertyAsync(owner, Address.Create("Main Street 1", null, "Springfield", null, "IE"));

        await using var reader = api.Persistence.CreateScope(owner);
        var saved = await reader.Db.Properties.SingleAsync(p => p.Id == property.Id, _ct);
        Assert.Null(saved.Address.Line2);
        Assert.Null(saved.Address.PostalCode);
    }

    [Fact]
    public async Task PropertyAndUnit_OfAnotherOrganization_AreInvisible()
    {
        var owner = await InNewOrganizationAsync();
        var property = await SavePropertyAsync(owner);
        var unit = await SaveUnitAsync(owner, property.Id, "4B");

        Assert.Equal((true, true), await VisibleAsync(owner, property.Id, unit.Id));
        Assert.Equal((false, false), await VisibleAsync(await InNewOrganizationAsync(), property.Id, unit.Id));
        Assert.Equal((false, false), await VisibleAsync(TestCurrentUser.WithoutOrganization(), property.Id, unit.Id));
    }

    [Fact]
    public async Task PropertyAndUnit_Archived_AreHiddenButKept()
    {
        var owner = await InNewOrganizationAsync();
        var property = await SavePropertyAsync(owner);
        var unit = await SaveUnitAsync(owner, property.Id, "4B");

        await using (var scope = api.Persistence.CreateScope(owner))
        {
            (await scope.Db.Units.SingleAsync(u => u.Id == unit.Id, _ct)).Archive(FixFlowApiFactory.SharedNow);
            (await scope.Db.Properties.SingleAsync(p => p.Id == property.Id, _ct)).Archive(FixFlowApiFactory.SharedNow);
            await scope.Db.SaveChangesAsync(_ct);
        }

        Assert.Equal((false, false), await VisibleAsync(owner, property.Id, unit.Id));

        // Still there, only filtered out
        await using var reader = api.Persistence.CreateScope(owner);
        Assert.True(await reader.Db.Properties
            .IgnoreQueryFilters([QueryFilters.SoftDelete])
            .AnyAsync(p => p.Id == property.Id && p.DeletedAt == FixFlowApiFactory.SharedNow, _ct));
        Assert.True(await reader.Db.Units
            .IgnoreQueryFilters([QueryFilters.SoftDelete])
            .AnyAsync(u => u.Id == unit.Id && u.DeletedAt == FixFlowApiFactory.SharedNow, _ct));
    }

    [Fact]
    public async Task Unit_SameNameInPropertyIgnoringCase_ViolatesUniqueIndex()
    {
        var owner = await InNewOrganizationAsync();
        var property = await SavePropertyAsync(owner);
        await SaveUnitAsync(owner, property.Id, "Apt 4B");

        await using var scope = api.Persistence.CreateScope(owner);
        scope.Db.Units.Add(Unit.Create(property.Id, "apt 4b"));

        await DatabaseAssertions.AssertUniqueViolationAsync(UniqueIndexes.UnitName, () => scope.Db.SaveChangesAsync(_ct));
    }

    [Fact]
    public async Task Unit_SameNameAsArchivedUnit_Saves()
    {
        var owner = await InNewOrganizationAsync();
        var property = await SavePropertyAsync(owner);
        var archived = await SaveUnitAsync(owner, property.Id, "4B");
        await using (var scope = api.Persistence.CreateScope(owner))
        {
            (await scope.Db.Units.SingleAsync(u => u.Id == archived.Id, _ct)).Archive(FixFlowApiFactory.SharedNow);
            await scope.Db.SaveChangesAsync(_ct);
        }

        var unit = await SaveUnitAsync(owner, property.Id, "4B");

        Assert.Equal((true, true), await VisibleAsync(owner, property.Id, unit.Id));
    }

    [Fact]
    public async Task Unit_SameNameInAnotherProperty_Saves()
    {
        var owner = await InNewOrganizationAsync();
        var first = await SavePropertyAsync(owner);
        var second = await SavePropertyAsync(owner);
        await SaveUnitAsync(owner, first.Id, "4B");

        var unit = await SaveUnitAsync(owner, second.Id, "4B");

        Assert.Equal((true, true), await VisibleAsync(owner, second.Id, unit.Id));
    }

    /// <summary>A caller in a new, saved organization: properties and units have a foreign key to it.</summary>
    private async Task<TestCurrentUser> InNewOrganizationAsync() =>
        TestCurrentUser.InOrganization(await api.Persistence.SaveOrganizationAsync());

    private async Task<Property> SavePropertyAsync(TestCurrentUser owner, Address? address = null)
    {
        await using var scope = api.Persistence.CreateScope(owner);
        var property = Property.Create(
            TestData.Name("Property"),
            address ?? Address.Create("Main Street 1", null, "Springfield", "00-001", "PL"),
            "Europe/Warsaw");
        scope.Db.Properties.Add(property);
        await scope.Db.SaveChangesAsync(_ct);
        return property;
    }

    private async Task<Unit> SaveUnitAsync(TestCurrentUser owner, Guid propertyId, string name)
    {
        await using var scope = api.Persistence.CreateScope(owner);
        var unit = Unit.Create(propertyId, name);
        scope.Db.Units.Add(unit);
        await scope.Db.SaveChangesAsync(_ct);
        return unit;
    }

    private async Task<(bool Property, bool Unit)> VisibleAsync(TestCurrentUser caller, Guid propertyId, Guid unitId)
    {
        await using var scope = api.Persistence.CreateScope(caller);
        return (
            await scope.Db.Properties.AnyAsync(p => p.Id == propertyId, _ct),
            await scope.Db.Units.AnyAsync(u => u.Id == unitId, _ct));
    }
}
