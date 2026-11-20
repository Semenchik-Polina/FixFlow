using System.Linq.Expressions;
using FixFlow.Domain.Properties;

namespace FixFlow.Application.Properties;

/// <summary>A unit as the unit endpoints return it.</summary>
public sealed record UnitResponse(Guid Id, Guid PropertyId, string Name)
{
    /// <summary>The one mapping: queries project with it, and <see cref="From"/> runs it on a loaded unit.</summary>
    internal static readonly Expression<Func<Unit, UnitResponse>> Projection =
        unit => new UnitResponse(unit.Id, unit.PropertyId, unit.Name);

    private static readonly Func<Unit, UnitResponse> _from = Projection.Compile();

    internal static UnitResponse From(Unit unit) => _from(unit);
}
