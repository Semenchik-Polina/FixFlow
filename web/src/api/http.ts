import { globalPaths, orgSlugFromPath } from '../shared/routing/paths';
import { ApiError } from './ApiError';
import { redirectToLogin } from './navigation';

const ORGANIZATION_HEADER = 'X-Organization';

/**
 * The error type of every generated query and mutation (orval reads it from this file). orval passes the declared
 * error body; whatever it is, the call rejects with an ApiError.
 */
// eslint-disable-next-line @typescript-eslint/no-unused-vars -- the parameter is orval's contract
export type ErrorType<_ResponseError> = ApiError;

/**
 * The fetch every generated API call goes through (orval.config.ts):
 * - sends the session cookie; auth is cookie-only, never a token (CLAUDE.md §3.8)
 * - sends `X-Organization` with the slug of the current page URL, and only that: a header passed by the caller is
 *   dropped, and global pages such as /login send none (ARCHITECTURE §8)
 * - rejects with an {@link ApiError} for every error response; a 401 also sends the browser to the login page
 */
export async function http<T>(url: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  headers.delete(ORGANIZATION_HEADER);
  const orgSlug = orgSlugFromPath(window.location.pathname);
  if (orgSlug !== null) {
    headers.set(ORGANIZATION_HEADER, orgSlug);
  }

  // Resolved against the page's origin: the API is same-origin, and Node's fetch (in tests) needs an absolute URL
  const response = await fetch(new URL(url, window.location.origin), { ...init, headers, credentials: 'include' });

  if (!response.ok) {
    if (response.status === 401 && window.location.pathname !== globalPaths.login) {
      redirectToLogin();
    }

    throw await ApiError.fromResponse(response);
  }

  const body = await response.text();
  return (body === '' ? undefined : JSON.parse(body)) as T;
}
