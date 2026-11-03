import { SLUG_MAX_LENGTH } from '../forms/rules';

// Latin letters that Unicode doesn't decompose into a base letter and an accent
const SPELLED_OUT: Readonly<Record<string, string>> = { ß: 'ss', æ: 'ae', œ: 'oe', ø: 'o', ł: 'l', đ: 'd', þ: 'th' };

/**
 * A slug for an organization named `name`: its words in lowercase ASCII, joined by single hyphens, within the slug's
 * length ("Brightway Homes & Co." → `brightway-homes-co`, "Łódź Café" → `lodz-cafe`). Characters with no ASCII form are
 * dropped, so a name in another script gives `''`. Only a suggestion: the user can edit it, and the server decides.
 */
export function suggestSlug(name: string): string {
  const words =
    name
      .toLowerCase()
      .replace(/[ßæœøłđþ]/g, (letter) => SPELLED_OUT[letter] ?? '')
      .normalize('NFKD')
      .replace(/\p{M}/gu, '')
      .match(/[a-z0-9]+/g) ?? [];

  // A cut can end on the hyphen between two words
  return words.join('-').slice(0, SLUG_MAX_LENGTH).replace(/-$/, '');
}
