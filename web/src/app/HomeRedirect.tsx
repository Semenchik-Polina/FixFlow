import Box from '@mui/material/Box';
import { Navigate } from 'react-router';
import { LoadError } from '../shared/components/LoadError';
import { PageLoader } from '../shared/components/PageLoader';
import { globalPaths } from '../shared/routing/paths';
import { useCurrentUser } from '../shared/session/useCurrentUser';

/**
 * `/`, where signing in lands: the user's only organization, the picker when there are several, and creating one when
 * there are none. Without a session, `http.ts` sends the browser to the login page.
 */
export function HomeRedirect() {
  const { data: me, error, refetch } = useCurrentUser();

  if (me === undefined) {
    return error !== null ? (
      <Box sx={{ p: 2 }}>
        <LoadError error={error} onRetry={refetch} />
      </Box>
    ) : (
      <PageLoader />
    );
  }

  const [first, ...others] = me.memberships;
  if (first === undefined) {
    return <Navigate to={globalPaths.newOrganization} replace />;
  }

  return <Navigate to={others.length === 0 ? `/${first.slug}` : globalPaths.organizations} replace />;
}
