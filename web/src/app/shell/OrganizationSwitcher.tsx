import ArrowDropDown from '@mui/icons-material/ArrowDropDown';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Divider from '@mui/material/Divider';
import ListItemText from '@mui/material/ListItemText';
import Menu from '@mui/material/Menu';
import MenuItem from '@mui/material/MenuItem';
import { useId, useState } from 'react';
import { Link as RouterLink } from 'react-router';
import { globalPaths } from '../../shared/routing/paths';
import { useCurrentOrganization } from '../../shared/session/useCurrentOrganization';
import { useCurrentUser } from '../../shared/session/useCurrentUser';
import { strings } from '../../shared/strings';

/**
 * The current organization's name, the largest thing in the app bar, and the menu to switch to another one. Switching
 * opens the other organization's home: a record of one organization means nothing in another.
 */
export function OrganizationSwitcher() {
  const menuId = useId();
  const { slug, organizationName } = useCurrentOrganization();
  const memberships = useCurrentUser().data?.memberships ?? [];
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  const open = anchor !== null;
  const close = () => setAnchor(null);

  return (
    <>
      <Button
        color="inherit"
        endIcon={<ArrowDropDown />}
        aria-label={strings.shell.switchOrganization(organizationName)}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-controls={open ? menuId : undefined}
        onClick={(event) => setAnchor(event.currentTarget)}
        sx={{ minWidth: 0, typography: 'subtitle1', fontWeight: 600 }}
      >
        <Box component="span" sx={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
          {organizationName}
        </Box>
      </Button>
      <Menu
        id={menuId}
        anchorEl={anchor}
        open={open}
        onClose={close}
        slotProps={{ list: { 'aria-label': strings.shell.organizations } }}
      >
        {memberships.map((membership) => (
          <MenuItem
            key={membership.slug}
            component={RouterLink}
            to={`/${membership.slug}`}
            selected={membership.slug === slug}
            onClick={close}
          >
            <ListItemText
              primary={membership.organizationName}
              secondary={membership.roles.map((role) => strings.roles[role]).join(', ')}
            />
          </MenuItem>
        ))}
        <Divider />
        <MenuItem component={RouterLink} to={globalPaths.organizations} onClick={close}>
          {strings.shell.allOrganizations}
        </MenuItem>
        <MenuItem component={RouterLink} to={globalPaths.newOrganization} onClick={close}>
          {strings.shell.createOrganization}
        </MenuItem>
      </Menu>
    </>
  );
}
