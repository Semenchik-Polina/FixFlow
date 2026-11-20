using FixFlow.Application.Auth;
using FixFlow.Application.Persistence;
using FixFlow.Application.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FixFlow.Application.Properties.GetProperty;

internal sealed class GetPropertyHandler(IFixFlowDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetPropertyQuery, Result<PropertyResponse>>
{
    public async Task<Result<PropertyResponse>> Handle(GetPropertyQuery query, CancellationToken cancellationToken)
    {
        var property = await db.Properties
            .AsNoTracking()
            .VisibleTo(currentUser)
            .Where(property => property.Id == query.Id)
            .Select(PropertyResponse.Projection)
            .SingleOrDefaultAsync(cancellationToken);
        if (property is null)
        {
            return Result.NotFound();
        }

        return property;
    }
}
