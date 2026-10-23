using NodaTime;

namespace FixFlow.Domain.Organizations;

/// <summary>
/// The SaaS customer and the data boundary (D2). It isn't <see cref="IOrganizationOwned"/>: it is the boundary, and
/// the organization context (T1.5) finds it by slug before any context exists. The slug never changes (A10); it is
/// unique across all organizations (§10), which the database enforces, not this aggregate.
/// </summary>
public sealed class Organization : AggregateRoot, IAuditable
{
    public const int NameMaxLength = 100;

    private Organization(string name, string slug)
    {
        Name = name;
        Slug = slug;
    }

    // For EF Core
    private Organization()
    {
        Name = "";
        Slug = "";
    }

    public string Name { get; private set; }

    public string Slug { get; private set; }

    public Instant CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public Instant? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    /// <param name="name">Trimmed; required, at most <see cref="NameMaxLength"/> characters.</param>
    /// <param name="slug">Taken as written; see <see cref="OrganizationSlug.IsValid"/>.</param>
    /// <exception cref="DomainException">The name or the slug isn't valid.</exception>
    public static Organization Create(string name, string slug)
    {
        var validName = ValidName(name);

        if (!OrganizationSlug.IsValid(slug, out var slugError))
        {
            throw new DomainException(slugError);
        }

        return new Organization(validName, slug);
    }

    private static string ValidName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            throw new DomainException("An organization needs a name.");
        }

        if (trimmed.Length > NameMaxLength)
        {
            throw new DomainException($"An organization's name can't be longer than {NameMaxLength} characters.");
        }

        return trimmed;
    }
}
