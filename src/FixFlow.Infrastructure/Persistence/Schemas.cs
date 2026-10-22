namespace FixFlow.Infrastructure.Persistence;

/// <summary>
/// One PostgreSQL schema per module (§10). Every entity configuration maps its table into one of these:
/// <c>builder.ToTable("work_orders", Schemas.WorkOrders)</c>. Nothing lives in <c>public</c>.
/// </summary>
public static class Schemas
{
    public const string Identity = "identity";

    public const string Organizations = "organizations";

    public const string Properties = "properties";

    public const string Requests = "requests";

    public const string WorkOrders = "workorders";

    /// <summary>Not a module: EF Core's migrations history table.</summary>
    public const string Migrations = "migrations";
}
