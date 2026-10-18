import { describe, expect, it, vi } from 'vitest';
import { formatInstant } from './formatInstant';

// Newer ICU puts a narrow no-break space before AM/PM; compare with plain spaces
function format(...args: Parameters<typeof formatInstant>): string {
  return formatInstant(...args).replace(/\s/g, ' ');
}

const TIME_WITH_OFFSET: Intl.DateTimeFormatOptions = {
  hour: 'numeric',
  minute: '2-digit',
  timeZoneName: 'shortOffset',
};

describe('formatInstant', () => {
  describe('Europe/Kyiv, summer time ends on 2026-10-25 (04:00 EEST → 03:00 EET)', () => {
    it('shows noon UTC one hour earlier in local time after the change', () => {
      expect(format('2026-10-24T12:00:00Z', 'Europe/Kyiv')).toBe('Oct 24, 2026, 3:00 PM');
      expect(format('2026-10-26T12:00:00Z', 'Europe/Kyiv')).toBe('Oct 26, 2026, 2:00 PM');
    });

    it('shows the repeated hour twice, told apart by the offset', () => {
      expect(format('2026-10-25T00:30:00Z', 'Europe/Kyiv', TIME_WITH_OFFSET)).toBe('3:30 AM GMT+3');
      expect(format('2026-10-25T01:30:00Z', 'Europe/Kyiv', TIME_WITH_OFFSET)).toBe('3:30 AM GMT+2');
    });
  });

  describe('America/New_York, summer time starts on 2026-03-08 (02:00 EST → 03:00 EDT)', () => {
    it('shows noon UTC one hour later in local time after the change', () => {
      expect(format('2026-03-07T17:00:00Z', 'America/New_York')).toBe('Mar 7, 2026, 12:00 PM');
      expect(format('2026-03-09T17:00:00Z', 'America/New_York')).toBe('Mar 9, 2026, 1:00 PM');
    });

    it('skips the missing hour', () => {
      expect(format('2026-03-08T06:59:00Z', 'America/New_York', TIME_WITH_OFFSET)).toBe('1:59 AM GMT-5');
      expect(format('2026-03-08T07:00:00Z', 'America/New_York', TIME_WITH_OFFSET)).toBe('3:00 AM GMT-4');
    });
  });

  it('formats one instant in the zone it is given', () => {
    expect(format('2026-10-24T12:00:00Z', 'Europe/Kyiv')).toBe('Oct 24, 2026, 3:00 PM');
    expect(format('2026-10-24T12:00:00Z', 'America/New_York')).toBe('Oct 24, 2026, 8:00 AM');
    expect(format('2026-10-24T12:00:00Z', 'UTC')).toBe('Oct 24, 2026, 12:00 PM');
  });

  it('creates one formatter per zone and format', () => {
    const constructor = vi.spyOn(Intl, 'DateTimeFormat');

    format('2026-10-24T12:00:00Z', 'Asia/Tokyo');
    format('2026-10-25T12:00:00Z', 'Asia/Tokyo');
    format('2026-10-24T12:00:00Z', 'Asia/Tokyo', TIME_WITH_OFFSET);

    expect(constructor).toHaveBeenCalledTimes(2);
  });

  it('rejects an unknown zone and an invalid instant', () => {
    expect(() => formatInstant('2026-10-24T12:00:00Z', 'Europe/Atlantis')).toThrow(RangeError);
    expect(() => formatInstant('not a date', 'Europe/Kyiv')).toThrow(RangeError);
  });
});
