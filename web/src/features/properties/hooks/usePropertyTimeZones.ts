import { useListPropertyTimeZones } from '../../../api/generated';
import type { CountryTimeZones } from '../../../api/generated/model';
import { countryName } from '../countryName';

export interface PropertyTimeZones {
  /** Every country code, sorted by the country's name. */
  countries: string[];
  /** Each country's time zones, as the server lists them. */
  zonesByCountry: ReadonlyMap<string, readonly string[]>;
}

/**
 * The countries a property can be in and each one's time zones (`GET /properties/time-zones`). They change only with
 * a new API build, so they're read once per organization.
 */
export function usePropertyTimeZones() {
  return useListPropertyTimeZones({ query: { staleTime: Infinity, select: toPropertyTimeZones } });
}

// Module-level, so the query keeps the selected value between renders
function toPropertyTimeZones(countries: CountryTimeZones[]): PropertyTimeZones {
  return {
    countries: countries
      .map((country) => country.countryCode)
      .toSorted((a, b) => countryName(a).localeCompare(countryName(b), 'en-US')),
    zonesByCountry: new Map(countries.map((country) => [country.countryCode, country.timeZones])),
  };
}
