import { useQuery } from '@tanstack/react-query';
import { getGetMeQueryOptions, getMe } from '../../api/generated';
import { useGlobalQueryClient } from './globalQueryClient';

/**
 * The session's query (`GET /me`), for the hook and for the routes that start it early. Its request doesn't take the
 * query's abort signal: TanStack aborts a request that does once no component reads the query any more, and the
 * session passes from page to page (`/` hands it to the organization's layout, and in development StrictMode remounts
 * every page), so an abort would only send it again, and leave a cancelled request in the API's logs.
 */
export function sessionQueryOptions() {
  return getGetMeQueryOptions({ query: { queryFn: () => getMe() } });
}

/**
 * The signed-in user and their active memberships (`GET /me`), always from the global query client, so every
 * organization's pages share one copy of the session. Without a session, `http.ts` sends the browser to the login page.
 */
export function useCurrentUser() {
  return useQuery(sessionQueryOptions(), useGlobalQueryClient());
}
