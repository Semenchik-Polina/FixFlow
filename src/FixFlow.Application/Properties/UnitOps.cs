using FixFlow.Application.Auth;

namespace FixFlow.Application.Properties;

/// <summary>What a caller does to a unit, checked by <see cref="UnitAuthorizationHandler"/>.</summary>
public static class UnitOps
{
    public static readonly Operation Rename = new("Unit.Rename");

    public static readonly Operation Archive = new("Unit.Archive");
}
