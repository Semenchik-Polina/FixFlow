import { useLogOut } from '../../api/generated';
import { redirectToLogin } from '../../api/navigation';

/** Ends the session, then loads the login page from scratch, which drops every cached query of the session. */
export function useSignOut() {
  const logOut = useLogOut();
  return {
    signOut: () => logOut.mutate(undefined, { onSuccess: redirectToLogin }),
    isPending: logOut.isPending,
  };
}
