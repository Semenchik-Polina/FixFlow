import { HttpResponse } from 'msw';
import { getGetMeMockHandler, getListPropertyTimeZonesMockHandler } from '../api/generated/fixflow-api/fixflow-api.msw';
import type { MeResponse, MyMembership, PropertyResponse, Role, UnitResponse } from '../api/generated/model';
import { server } from './server';

// Fake data only (D23): example.test addresses, made-up names

let nextOrganization = 1;

/** An active membership in the organization `slug`, named after it (`brightway-homes` → "Brightway Homes"). */
export function aMembership(slug: string, roles: Role[] = ['Manager']): MyMembership {
  const number = String(nextOrganization++).padStart(12, '0');
  return {
    organizationId: `0199b1c2-0000-7000-8000-${number}`,
    organizationName: slug
      .split('-')
      .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
      .join(' '),
    slug,
    roles,
  };
}

/** The signed-in user as `/me` describes them, with these memberships. */
export function aUser(...memberships: MyMembership[]): MeResponse {
  return {
    id: '0199b1c2-0000-7000-8000-0000000000aa',
    email: 'pat.kim@example.test',
    firstName: 'Pat',
    lastName: 'Kim',
    memberships,
  };
}

/** The API answers `/me` for a user with these memberships. */
export function givenSignedIn(...memberships: MyMembership[]): MeResponse {
  const me = aUser(...memberships);
  server.use(getGetMeMockHandler(me));
  return me;
}

/** An error response as the API sends it: Problem Details. */
export function problem(status: number, body: Record<string, unknown> = {}) {
  return HttpResponse.json({ status, ...body }, { status, headers: { 'Content-Type': 'application/problem+json' } });
}

/** A validation error with field errors, keyed by the camelCase field name; a 422 for the ones only the server knows. */
export function validationProblem(errors: Record<string, string[]>, status = 400) {
  return problem(status, { title: 'One or more validation errors occurred.', errors });
}

/** A property as the API returns it: in Poland, with its one zone. */
export function aProperty(overrides: Partial<PropertyResponse> = {}): PropertyResponse {
  return {
    id: '0199b1c2-0000-7000-8000-000000000301',
    name: 'Riverside Apartments',
    address: { line1: '1 Test Street', line2: null, city: 'Testville', postalCode: '00-001', countryCode: 'PL' },
    timeZone: 'Europe/Warsaw',
    ...overrides,
  };
}

let nextUnit = 1;

/** A unit of `propertyId` named `name`, with an id of its own. */
export function aUnit(propertyId: string, name: string): UnitResponse {
  const number = String(nextUnit++).padStart(12, '0');
  // 9000: apart from the memberships' 8000 ids
  return { id: `0199b1c2-0000-7000-9000-${number}`, propertyId, name };
}

/** One page of a list endpoint, as `PagedResult` has it. */
export function aPage<T>(items: T[], { page = 1, pageSize = 25, totalCount = items.length } = {}) {
  return { items, page, pageSize, totalCount };
}

/**
 * The API lists these countries and zones for the property form: Poland with one zone, the US with several. Call it
 * after any `getGetPropertyMockHandler`: a handler added later wins, and `properties/:id` matches `time-zones` too.
 */
export function givenPropertyTimeZones(): void {
  server.use(
    getListPropertyTimeZonesMockHandler([
      { countryCode: 'PL', timeZones: ['Europe/Warsaw'] },
      { countryCode: 'UA', timeZones: ['Europe/Kyiv', 'Europe/Simferopol'] },
      {
        countryCode: 'US',
        timeZones: ['America/Chicago', 'America/Denver', 'America/Los_Angeles', 'America/New_York'],
      },
    ]),
  );
}
