// English only for now (D27); localization replaces this with the user's locale
const LOCALE = 'en-US';

const DEFAULT_FORMAT: Intl.DateTimeFormatOptions = { dateStyle: 'medium', timeStyle: 'short' };

// Creating a DateTimeFormat costs far more than using one, and a list formats many dates in a few zones and formats
const formatters = new Map<string, Intl.DateTimeFormat>();

/**
 * Formats an instant from the API (ISO 8601 UTC, e.g. `2026-10-25T01:30:00Z`) as wall-clock time in an IANA time zone.
 * For anything building-related that is the property's zone, never the browser's (ARCHITECTURE §11), so the zone is
 * required. `options` replaces the default format. Throws a RangeError for an invalid instant or an unknown zone.
 */
export function formatInstant(
  instant: string,
  timeZone: string,
  options: Intl.DateTimeFormatOptions = DEFAULT_FORMAT,
): string {
  const key = `${timeZone}|${JSON.stringify(options)}`;
  let formatter = formatters.get(key);
  if (formatter === undefined) {
    formatter = new Intl.DateTimeFormat(LOCALE, { ...options, timeZone });
    formatters.set(key, formatter);
  }

  return formatter.format(new Date(instant));
}
