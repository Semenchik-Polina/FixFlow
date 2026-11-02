import Button from '@mui/material/Button';
import List from '@mui/material/List';
import ListItemButton from '@mui/material/ListItemButton';
import ListItemText from '@mui/material/ListItemText';
import Skeleton from '@mui/material/Skeleton';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import { Link as RouterLink } from 'react-router';
import { GlobalPageLayout } from '../../../shared/components/GlobalPageLayout';
import { LoadError } from '../../../shared/components/LoadError';
import { globalPaths } from '../../../shared/routing/paths';
import { SignedInAs } from '../../../shared/session/SignedInAs';
import { useCurrentUser } from '../../../shared/session/useCurrentUser';
import { strings } from '../../../shared/strings';
import { organizationStrings } from '../strings';

/** Every organization the user may enter, for a user with several, and the way to create another. */
export function OrganizationPickerPage() {
  const { data: me, error, refetch } = useCurrentUser();
  const hasOrganizations = me !== undefined && me.memberships.length > 0;

  return (
    <GlobalPageLayout title={organizationStrings.picker.title}>
      {me === undefined ? (
        error !== null ? (
          <LoadError error={error} onRetry={refetch} />
        ) : (
          <Stack sx={{ gap: 1 }}>
            <Skeleton variant="rounded" height={64} />
            <Skeleton variant="rounded" height={64} />
          </Stack>
        )
      ) : hasOrganizations ? (
        <>
          <Typography sx={{ mb: 3 }}>{organizationStrings.picker.intro}</Typography>
          <List disablePadding sx={{ borderTop: 1, borderColor: 'divider' }}>
            {me.memberships.map(({ slug, organizationName, roles }) => (
              <ListItemButton key={slug} component={RouterLink} to={`/${slug}`} divider>
                <ListItemText
                  primary={organizationName}
                  secondary={roles.map((role) => strings.roles[role]).join(', ')}
                  slotProps={{ primary: { sx: { fontWeight: 600 } } }}
                />
              </ListItemButton>
            ))}
          </List>
        </>
      ) : (
        <Typography>{organizationStrings.picker.empty}</Typography>
      )}
      <Button
        component={RouterLink}
        to={globalPaths.newOrganization}
        variant={hasOrganizations ? 'outlined' : 'contained'}
        sx={{ mt: 4 }}
      >
        {organizationStrings.picker.create}
      </Button>
      <SignedInAs />
    </GlobalPageLayout>
  );
}
