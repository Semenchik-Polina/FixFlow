/**
 * Top-level pages that live outside any organization. Every other first path segment is an organization slug
 * (`/{orgSlug}/...`, ARCHITECTURE §8). The router builds the global routes from this list, so the two can't drift.
 * The backend's reserved slugs (`OrganizationSlug.Reserved`) must include each of these segments, and a domain test
 * checks it. Slugs never change, so a new page takes a name that list already reserves.
 */
export const globalPaths = {
  login: '/login',
} as const;

const globalSegments: ReadonlySet<string> = new Set(Object.values(globalPaths).map((path) => path.slice(1)));

/** The organization slug in an app URL, or null on a global page. */
export function orgSlugFromPath(pathname: string): string | null {
  const segment = pathname.split('/')[1] ?? '';
  // The router matches paths case-insensitively, so /LOGIN is the login page too
  if (segment === '' || globalSegments.has(segment.toLowerCase())) {
    return null;
  }

  return segment;
}
