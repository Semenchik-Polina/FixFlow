using FixFlow.Application.Results;
using FixFlow.Domain.Properties;
using MediatR;

namespace FixFlow.Application.Properties.ListPropertyTimeZones;

/// <summary>
/// The time zones a property can have, by country (<see cref="PropertyTimeZone.ByCountry"/>), sorted by country code.
/// The same for every organization: the SPA's property form offers the chosen country's zones from it. It takes
/// nothing, so there is no validator.
/// </summary>
public sealed record ListPropertyTimeZonesQuery : IRequest<Result<CountryTimeZones[]>>;

/// <summary>A country and the time zones a property there can have.</summary>
/// <param name="CountryCode">ISO 3166 alpha-2, uppercase: <c>PL</c>.</param>
/// <param name="TimeZones">Its zone.tab ids, sorted: <c>Europe/Warsaw</c>.</param>
public sealed record CountryTimeZones(string CountryCode, IReadOnlyList<string> TimeZones);
