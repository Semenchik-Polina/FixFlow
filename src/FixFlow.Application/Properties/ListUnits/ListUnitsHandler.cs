using FixFlow.Application.Auth;
using FixFlow.Application.Paging;
using FixFlow.Application.Persistence;
using FixFlow.Application.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FixFlow.Application.Properties.ListUnits;

internal sealed class ListUnitsHandler(IFixFlowDbContext db, ICurrentUser currentUser)
    : IRequestHandler<ListUnitsQuery, Result<PagedResult<UnitResponse>>>
{
    public async Task<Result<PagedResult<UnitResponse>>> Handle(ListUnitsQuery query, CancellationToken cancellationToken)
    {
        // The filters don't look at a unit's property, so an archived or foreign property is a 404, not an empty page
        if (!await db.Properties.VisibleTo(currentUser).AnyAsync(property => property.Id == query.PropertyId, cancellationToken))
        {
            return Result.NotFound();
        }

        return await db.Units
            .AsNoTracking()
            .VisibleTo(currentUser)
            .Where(unit => unit.PropertyId == query.PropertyId)
            .OrderBy(unit => unit.Name)
            .Select(UnitResponse.Projection)
            .ToPageAsync(query, cancellationToken);
    }
}
