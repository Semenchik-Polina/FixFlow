import { useLogOut } from '../../api/generated';
import { redirectToLogin } from '../../api/navigation';

/**
 * Ends the session, then loads the login page from scratch, which drops every cached query of the session. A failed
 * request sets `failed` and stays put: the cookie still works, so the login page would only look signed out (on a
 * shared device, too). A session that already ended answers 401, and http.ts loads the login page for that.
 */
export function useSignOut() {
  const logOut = useLogOut();
  return {
    signOut: () => logOut.mutate(undefined, { onSuccess: redirectToLogin }),
    isPending: logOut.isPending,
    failed: logOut.isError,
  };
}
