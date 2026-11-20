using System.Diagnostics.CodeAnalysis;
using FixFlow.Application.Auth;
using FixFlow.Application.Paging;
using FixFlow.Application.Persistence;
using FixFlow.Application.Results;
using FixFlow.Domain.Properties;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FixFlow.Application.Properties.ListProperties;

internal sealed class ListPropertiesHandler(IFixFlowDbContext db, ICurrentUser currentUser)
    : IRequestHandler<ListPropertiesQuery, Result<PagedResult<PropertyResponse>>>
{
    private const string TranslatedToSql =
        "SQL runs it: Npgsql translates ToUpper() to upper(), and none of the overloads with a culture or a comparison";

    public async Task<Result<PagedResult<PropertyResponse>>> Handle(
        ListPropertiesQuery query, CancellationToken cancellationToken)
    {
        var properties = db.Properties.AsNoTracking().VisibleTo(currentUser);

        var name = query.Name?.Trim();
        if (!string.IsNullOrEmpty(name))
        {
            properties = NameContains(properties, name.ToUpperInvariant());
        }

        return await properties
            // The id breaks ties: names aren't unique
            .OrderBy(property => property.Name)
            .ThenBy(property => property.Id)
            .Select(PropertyResponse.Projection)
            .ToPageAsync(query, cancellationToken);
    }

    /// <summary>
    /// <c>strpos(upper(name), @term) &gt; 0</c>: a plain substring, so <c>%</c> and <c>_</c> match themselves.
    /// </summary>
    /// <param name="term">Uppercased.</param>
    [SuppressMessage("Globalization", "CA1304", Justification = TranslatedToSql)]
    [SuppressMessage("Globalization", "CA1311", Justification = TranslatedToSql)]
    [SuppressMessage("Performance", "CA1862", Justification = TranslatedToSql)]
    private static IQueryable<Property> NameContains(IQueryable<Property> properties, string term) =>
        properties.Where(property => property.Name.ToUpper().Contains(term));
}
