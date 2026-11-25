# ADR 0008: A property's time zone must be one of its country's zones

- **Status:** Accepted
- **Date:** 2026-11-25
- **Amends:** P37 (`docs/plans/mvp.md`), `ARCHITECTURE.md` §11

## Context

P37 accepts any tzdb zone that names a place, with any country. Nothing stopped a property in Poland from having
`America/New_York`. Its SLAs, due dates and appointments would then run on the wrong clock (§11), and the mistake is
easy to make in a long zone list.

## Decision

1. **The zone must be one of the address country's zones** (`PropertyTimeZone.IsValidFor`). A zone matches when its
   canonical zone is the canonical zone of one of the country's zone.tab ids, or a zone1970.tab zone that names the
   country. So aliases (`Europe/Kiev` for UA) and the zone a country's id links to (`Europe/Berlin` for NO) pass.
2. `Property.Create` and `Update` enforce it, as a 422 `DomainException`. The create and update validators check it
   first, as a 400 field error on `timeZone`, but only when the zone and the country are each valid.
3. **The server lists each country's zones:** `GET /api/v1/properties/time-zones` (Managers and Admins), taken from
   zone.tab. The SPA's property form offers the chosen country's zones from it, instead of the browser's zone list.
   The browser's per-country list isn't in every browser and can disagree with NodaTime.

## Consequences

- A property's clock now follows its address. Changing the country means choosing a new zone.
- There's no schema change and no migration. A property saved before this rule with a mismatched pair (development
  data only, since nothing is released) has to fix its zone at its next edit.
- P37's "aliases stay" still holds for API clients and stored ids. The SPA no longer depends on aliases, because it
  offers zone.tab ids.
- Countries aren't restricted. Limiting where properties can be created would be a product rule of its own.
