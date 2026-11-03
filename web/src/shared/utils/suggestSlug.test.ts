import { describe, expect, it } from 'vitest';
import { suggestSlug } from './suggestSlug';

describe('suggestSlug', () => {
  it.each([
    ['Acme', 'acme'],
    ['Brightway Homes & Co.', 'brightway-homes-co'],
    ['  Riverside   Lofts  ', 'riverside-lofts'],
    ['Café Zürich', 'cafe-zurich'],
    ['Łódź Straße Ørsted', 'lodz-strasse-orsted'],
    ['Block 42-B', 'block-42-b'],
    ['Минск Мир', ''],
    ['', ''],
  ])('suggests %j for %j', (name, slug) => {
    expect(suggestSlug(name)).toBe(slug);
  });

  it('cuts at 40 characters without a trailing hyphen', () => {
    // 39 characters, then a hyphen at the 40th, then more
    const slug = suggestSlug(`${'a'.repeat(39)} bcd`);

    expect(slug).toBe('a'.repeat(39));
  });
});
