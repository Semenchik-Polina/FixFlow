using NodaTime;

namespace FixFlow.Domain.Properties;

/// <summary>
/// An apartment or space in a property (§7), which tenants rent and requests are about. Its name is unique in its
/// property among units that aren't archived, ignoring case; the database enforces that, not this aggregate.
/// </summary>
public sealed class Unit : AggregateRoot, IOrganizationOwned, ISoftDeletable, IAuditable
{
    public const int NameMaxLength = 50;

    private Unit(Guid propertyId, string name)
    {
        PropertyId = propertyId;
        Name = name;
    }

    // For EF Core
    private Unit() => Name = "";

    public Guid PropertyId { get; private set; }

    /// <summary>Its name or number, as the building uses it: "4B", "Office 3".</summary>
    public string Name { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Instant? DeletedAt { get; private set; }

    public Instant CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public Instant? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    /// <summary>A unit of the request's organization: persistence sets <see cref="OrganizationId"/>.</summary>
    /// <param name="propertyId">A property of the same organization, which the caller has loaded.</param>
    /// <param name="name">Trimmed; required, at most <see cref="NameMaxLength"/> characters, no control characters.</param>
    /// <exception cref="DomainException">The name isn't valid.</exception>
    public static Unit Create(Guid propertyId, string name)
    {
        if (propertyId == Guid.Empty)
        {
            throw new ArgumentException("A unit needs a property id.", nameof(propertyId));
        }

        return new Unit(propertyId, DomainText.Required(name, NameMaxLength, "A unit's name"));
    }

    /// <summary>Hides the unit from every query (soft delete); its name can then be used again.</summary>
    /// <exception cref="DomainException">The unit is already archived.</exception>
    public void Archive(Instant at)
    {
        if (DeletedAt is not null)
        {
            throw new DomainException("This unit is already archived.");
        }

        DeletedAt = at;
    }
}
