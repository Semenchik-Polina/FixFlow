import Alert from '@mui/material/Alert';
import Avatar from '@mui/material/Avatar';
import Box from '@mui/material/Box';
import Divider from '@mui/material/Divider';
import IconButton from '@mui/material/IconButton';
import ListSubheader from '@mui/material/ListSubheader';
import Menu from '@mui/material/Menu';
import MenuItem from '@mui/material/MenuItem';
import Typography from '@mui/material/Typography';
import { useId, useState } from 'react';
import { useCurrentUser } from '../../shared/session/useCurrentUser';
import { useSignOut } from '../../shared/session/useSignOut';
import { strings } from '../../shared/strings';

/** The user's initials in the app bar, opening who is signed in and the way to sign out. */
export function UserMenu() {
  const menuId = useId();
  const me = useCurrentUser().data;
  const { signOut, isPending, failed } = useSignOut();
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  const open = anchor !== null;

  if (me === undefined) {
    return null;
  }

  return (
    <>
      <IconButton
        aria-label={strings.shell.accountMenu}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-controls={open ? menuId : undefined}
        onClick={(event) => setAnchor(event.currentTarget)}
        edge="end"
      >
        <Avatar sx={{ width: 32, height: 32, bgcolor: 'secondary.main', typography: 'body2', fontWeight: 600 }}>
          {`${me.firstName.charAt(0)}${me.lastName.charAt(0)}`.toUpperCase()}
        </Avatar>
      </IconButton>
      <Menu
        id={menuId}
        anchorEl={anchor}
        open={open}
        onClose={() => setAnchor(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
      >
        {/* A subheader, which the menu's keyboard navigation skips: it's information, not an action */}
        <ListSubheader sx={{ lineHeight: 1.5, py: 1 }}>
          <Typography variant="body2" sx={{ fontWeight: 600, color: 'text.primary' }}>
            {me.firstName} {me.lastName}
          </Typography>
          <Typography variant="body2">{me.email}</Typography>
        </ListSubheader>
        <Divider />
        <MenuItem onClick={signOut} disabled={isPending}>
          {strings.session.signOut}
        </MenuItem>
        {/* No tabindex, so the menu's keyboard navigation skips it like the subheader */}
        {failed && (
          <Box component="li" sx={{ px: 2, py: 1, maxWidth: 320 }}>
            <Alert severity="error">{strings.session.signOutFailed}</Alert>
          </Box>
        )}
      </Menu>
    </>
  );
}
