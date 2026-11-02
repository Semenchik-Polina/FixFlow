import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import { Link as RouterLink } from 'react-router';
import { GlobalPageLayout } from '../../../shared/components/GlobalPageLayout';
import { globalPaths } from '../../../shared/routing/paths';
import { SignedInAs } from '../../../shared/session/SignedInAs';
import { organizationStrings } from '../strings';

interface OrganizationNotFoundPageProps {
  slug: string;
}

/**
 * The slug in the URL isn't one of the user's organizations. Like the API's 404, it doesn't say whether the
 * organization exists.
 */
export function OrganizationNotFoundPage({ slug }: OrganizationNotFoundPageProps) {
  return (
    <GlobalPageLayout title={organizationStrings.notFound.title}>
      <Typography sx={{ mb: 4 }}>{organizationStrings.notFound.detail(slug)}</Typography>
      <Button component={RouterLink} to={globalPaths.organizations} variant="contained">
        {organizationStrings.notFound.yourOrganizations}
      </Button>
      <SignedInAs />
    </GlobalPageLayout>
  );
}
