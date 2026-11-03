using System.Diagnostics.CodeAnalysis;
using NodaTime;

namespace FixFlow.Domain.Properties;

/// <summary>
/// A building or site the organization manages (§7). Its time zone is authoritative for SLAs, due dates and
/// appointments (§11). Its units are aggregates of their own, which reference it.
/// </summary>
[SuppressMessage("Naming", "CA1716", Justification = "The domain's name for it (§7); FixFlow has no Visual Basic callers")]
public sealed class Property : AggregateRoot, IOrganizationOwned, ISoftDeletable, IAuditable
{
    public const int NameMaxLength = 100;

    private Property(string name, Address address, string timeZoneId)
    {
        Name = name;
        Address = address;
        TimeZoneId = timeZoneId;
    }

    // For EF Core
    private Property()
    {
        Name = "";
        Address = null!;
        TimeZoneId = "";
    }

    public string Name { get; private set; }

    public Address Address { get; private set; }

    /// <summary>An IANA id, as written (<see cref="PropertyTimeZone"/>).</summary>
    public string TimeZoneId { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Instant? DeletedAt { get; private set; }

    public Instant CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public Instant? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    /// <summary>A property of the request's organization: persistence sets <see cref="OrganizationId"/>.</summary>
    /// <param name="name">Trimmed; required, at most <see cref="NameMaxLength"/> characters, no control characters.</param>
    /// <param name="address">See <see cref="Address.Create"/>.</param>
    /// <param name="timeZoneId">See <see cref="PropertyTimeZone.IsValid"/>.</param>
    /// <exception cref="DomainException">The name or the time zone isn't valid.</exception>
    public static Property Create(string name, Address address, string timeZoneId)
    {
        var validName = DomainText.Required(name, NameMaxLength, "A property's name");
        ArgumentNullException.ThrowIfNull(address);

        if (!PropertyTimeZone.IsValid(timeZoneId, out var timeZoneError))
        {
            throw new DomainException(timeZoneError);
        }

        return new Property(validName, address, timeZoneId);
    }

    /// <summary>Hides the property from every query (soft delete). Its units are archived separately.</summary>
    /// <exception cref="DomainException">The property is already archived.</exception>
    public void Archive(Instant at)
    {
        if (DeletedAt is not null)
        {
            throw new DomainException("This property is already archived.");
        }

        DeletedAt = at;
    }
}
