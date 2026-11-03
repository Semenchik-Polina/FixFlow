namespace FixFlow.Infrastructure.Persistence;

/// <summary>
/// Collations the model creates (<c>FixFlowDbContext.OnModelCreating</c>) for columns that compare differently from
/// the database's default: <c>builder.Property(...).UseCollation(Collations.CaseInsensitive)</c>.
/// </summary>
public static class Collations
{
    /// <summary>
    /// ICU, ignoring case but not accents, so "4B" and "4b" are one value to comparisons and unique indexes. It is
    /// nondeterministic, which PostgreSQL 18 supports in <c>LIKE</c> too.
    /// </summary>
    public const string CaseInsensitive = "case_insensitive";
}
