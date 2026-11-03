import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import { strings } from '../strings';
import { useCurrentUser } from './useCurrentUser';
import { useSignOut } from './useSignOut';

/** Who is signed in, and a way out, for the global pages of a signed-in user (the picker, a new organization). */
export function SignedInAs() {
  const email = useCurrentUser().data?.email;
  const { signOut, isPending, failed } = useSignOut();

  return (
    <Box sx={{ mt: 6 }}>
      <Box sx={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', columnGap: 1, color: 'text.secondary' }}>
        {email !== undefined && <Typography variant="body2">{strings.session.signedInAs(email)}</Typography>}
        <Button size="small" onClick={signOut} disabled={isPending}>
          {strings.session.signOut}
        </Button>
      </Box>
      {failed && (
        <Alert severity="error" sx={{ mt: 1 }}>
          {strings.session.signOutFailed}
        </Alert>
      )}
    </Box>
  );
}
