import Box from '@mui/material/Box';
import { useParams } from 'react-router';
import { OrganizationNotFoundPage } from '../../features/organizations/pages/OrganizationNotFoundPage';
import { LoadError } from '../../shared/components/LoadError';
import { PageLoader } from '../../shared/components/PageLoader';
import { findMembership } from '../../shared/session/useCurrentOrganization';
import { useCurrentUser } from '../../shared/session/useCurrentUser';
import { AppShell } from './AppShell';
import { OrganizationQueryScope } from './OrganizationQueryScope';

/**
 * `/:orgSlug`: an organization's pages, once the session shows the user belongs to it. The check is for the UX only;
 * the API checks the membership on every request (ARCHITECTURE §8).
 */
export function OrganizationLayout() {
  const { orgSlug = '' } = useParams();
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

  if (findMembership(me, orgSlug) === undefined) {
    return <OrganizationNotFoundPage slug={orgSlug} />;
  }

  // Keyed: another organization gets a new query cache (OrganizationQueryScope)
  return (
    <OrganizationQueryScope key={orgSlug}>
      <AppShell />
    </OrganizationQueryScope>
  );
}
