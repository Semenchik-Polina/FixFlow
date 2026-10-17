namespace FixFlow.Application.Persistence;

/// <summary>
/// Names of the global query filters, applied by convention to every entity with the matching marker interface.
/// Turning one off (<c>IgnoreQueryFilters([QueryFilters.SoftDelete])</c>) needs a comment justifying it, and the
/// organization filter almost never should be (CLAUDE.md §3.1).
/// </summary>
public static class QueryFilters
{
    /// <summary>Rows of the request's organization only (<c>IOrganizationOwned</c>).</summary>
    public const string Organization = nameof(Organization);

    /// <summary>Rows that aren't soft-deleted (<c>ISoftDeletable</c>).</summary>
    public const string SoftDelete = nameof(SoftDelete);
}
