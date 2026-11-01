import { matchPath } from 'react-router';

/**
 * Top-level pages that live outside any organization. Every other first path segment is an organization slug
 * (`/{orgSlug}/...`, ARCHITECTURE §8). The router builds the global routes from this list, so the two can't drift.
 * The backend's reserved slugs (`OrganizationSlug.Reserved`) must include each of these segments, and a domain test
 * checks it. Slugs never change, so a new page takes a name that list already reserves.
 */
export const globalPaths = {
  login: '/login',
  signup: '/signup',
  forgotPassword: '/forgot-password',
  resetPassword: '/reset-password',
  organizations: '/organizations',
  newOrganization: '/organizations/new',
} as const;

/** The first segment of a path: `'organizations'` for `/organizations/new`, `''` for `/`. */
export function firstSegment(pathname: string): string {
  return pathname.split('/')[1] ?? '';
}

// A global page's first segment is a reserved slug, so every path under it is global too
const globalSegments: ReadonlySet<string> = new Set(Object.values(globalPaths).map(firstSegment));

/** The organization slug in an app URL, or null on a global page. */
export function orgSlugFromPath(pathname: string): string | null {
  const segment = firstSegment(pathname);
  // The router matches paths case-insensitively, so /LOGIN is the login page too
  if (segment === '' || globalSegments.has(segment.toLowerCase())) {
    return null;
  }

  return segment;
}

// The pages of a signed-out visitor, which never need a session
const publicPaths: readonly string[] = [
  globalPaths.login,
  globalPaths.signup,
  globalPaths.forgotPassword,
  globalPaths.resetPassword,
];

/**
 * Whether the page at `pathname` is one for signed-out visitors (sign in, sign up, password reset). It matches the way
 * the router does, in any case and with or without a trailing slash, so `/LOGIN` and `/login/` are the login page.
 */
export function isPublicPage(pathname: string): boolean {
  return publicPaths.some((path) => matchPath({ path, caseSensitive: false, end: true }, pathname) !== null);
}
