import { HttpResponse } from 'msw';
import { getGetMeMockHandler } from '../api/generated/fixflow-api/fixflow-api.msw';
import type { MeResponse, MyMembership, Role } from '../api/generated/model';
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
