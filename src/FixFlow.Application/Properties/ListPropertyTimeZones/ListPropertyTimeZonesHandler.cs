using FixFlow.Application.Results;
using FixFlow.Domain.Properties;
using MediatR;

namespace FixFlow.Application.Properties.ListPropertyTimeZones;

internal sealed class ListPropertyTimeZonesHandler
    : IRequestHandler<ListPropertyTimeZonesQuery, Result<CountryTimeZones[]>>
{
    // The tzdb data is embedded, so the list can't change while the app runs. Nothing writes to the array.
    private static readonly CountryTimeZones[] _countries =
    [
        .. PropertyTimeZone.ByCountry
            .OrderBy(country => country.Key, StringComparer.Ordinal)
            .Select(country => new CountryTimeZones(country.Key, country.Value)),
    ];

    public Task<Result<CountryTimeZones[]>> Handle(
        ListPropertyTimeZonesQuery query, CancellationToken cancellationToken) =>
        Task.FromResult<Result<CountryTimeZones[]>>(_countries);
}
