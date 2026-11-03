using FixFlow.Domain;
using NodaTime;

namespace FixFlow.API.IntegrationTests.Persistence.TestModel;

/// <summary>A test-only aggregate with every persistence convention: org-owned, soft-deletable, audited, versioned.</summary>
public sealed class Widget : AggregateRoot, IOrganizationOwned, ISoftDeletable, IAuditable, IHasRowVersion
{
    /// <param name="organizationId">Left empty, persistence sets the request's organization.</param>
    public Widget(string name, Guid organizationId = default)
    {
        Name = name;
        OrganizationId = organizationId;
    }

    private Widget() => Name = "";

    public string Name { get; private set; }

    public int Bumps { get; private set; }

    public WidgetSize Size { get; private set; } = WidgetSize.Small;

    public Guid OrganizationId { get; private set; }

    public Instant? DeletedAt { get; private set; }

    public Instant CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public Instant? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public uint RowVersion { get; private set; }

    public void Rename(string name) => Name = name;

    public void Bump() => Bumps++;

    public void Delete(Instant at) => DeletedAt = at;

    /// <summary>Tests raise the test events (WidgetEvents.cs) directly.</summary>
    public void Publish(IDomainEvent domainEvent) => Raise(domainEvent);
}

/// <summary>An enum column, so the enum convention has something to check in the test model.</summary>
public enum WidgetSize
{
    Small,
    Large,
}
