// English only for now (D27), like formatInstant; localization replaces this with the user's locale
const regionNames = new Intl.DisplayNames('en-US', { type: 'region' });

/** A country's name from its ISO 3166 alpha-2 code: `PL` → "Poland". The code itself when the browser has no name. */
export function countryName(countryCode: string): string {
  return regionNames.of(countryCode) ?? countryCode;
}
