import { useParams } from 'react-router';
import type { MeResponse, MyMembership } from '../../api/generated/model';
import { useCurrentUser } from './useCurrentUser';

/** The user's active membership in the organization `slug`. Slugs are compared as written, as the API does. */
export function findMembership(me: MeResponse | undefined, slug: string | undefined): MyMembership | undefined {
  return slug === undefined ? undefined : me?.memberships.find((membership) => membership.slug === slug);
}

/**
 * The organization of the current page and the user's roles in it, from the URL's slug and `/me`. For pages under
 * `/:orgSlug` only: the organization layout renders them once it has found the membership.
 */
export function useCurrentOrganization(): MyMembership {
  const { orgSlug } = useParams();
  const membership = findMembership(useCurrentUser().data, orgSlug);
  if (membership === undefined) {
    throw new Error('useCurrentOrganization is used outside the pages of an organization the user belongs to.');
  }

  return membership;
}
