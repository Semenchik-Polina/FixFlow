import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import { strings } from '../strings';
import { useCurrentUser } from './useCurrentUser';
import { useSignOut } from './useSignOut';

/** Who is signed in, and a way out, for the global pages of a signed-in user (the picker, a new organization). */
export function SignedInAs() {
  const email = useCurrentUser().data?.email;
  const { signOut, isPending } = useSignOut();

  return (
    <Box sx={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', columnGap: 1, mt: 6, color: 'text.secondary' }}>
      {email !== undefined && <Typography variant="body2">{strings.session.signedInAs(email)}</Typography>}
      <Button size="small" onClick={signOut} disabled={isPending}>
        {strings.session.signOut}
      </Button>
    </Box>
  );
}
