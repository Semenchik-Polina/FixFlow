import MenuIcon from '@mui/icons-material/Menu';
import AppBar from '@mui/material/AppBar';
import Box from '@mui/material/Box';
import Drawer from '@mui/material/Drawer';
import IconButton from '@mui/material/IconButton';
import List from '@mui/material/List';
import ListItemButton from '@mui/material/ListItemButton';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import { useTheme } from '@mui/material/styles';
import Toolbar from '@mui/material/Toolbar';
import Typography from '@mui/material/Typography';
import useMediaQuery from '@mui/material/useMediaQuery';
import { useState } from 'react';
import { NavLink, Outlet } from 'react-router';
import { useCurrentOrganization } from '../../shared/session/useCurrentOrganization';
import { strings } from '../../shared/strings';
import { NavigationProgress } from './NavigationProgress';
import { visibleNavItems } from './navItems';
import { OrganizationSwitcher } from './OrganizationSwitcher';
import { UserMenu } from './UserMenu';

const DRAWER_WIDTH = 240;

/**
 * The layout of every organization page: app bar (the organization and its switcher, the user's menu), navigation
 * for the user's roles, and the page. Mobile-first: below `md` the navigation is a drawer behind a menu button, and
 * below `sm` the wordmark gives its room to the organization's name.
 */
export function AppShell() {
  const theme = useTheme();
  const isDesktop = useMediaQuery(theme.breakpoints.up('md'), { noSsr: true });
  const [mobileOpen, setMobileOpen] = useState(false);
  const { organizationName, roles } = useCurrentOrganization();

  return (
    <Box sx={{ display: 'flex', minHeight: '100dvh' }}>
      {/* The organization first, so tabs open in different organizations tell apart */}
      <title>{strings.pageTitle(organizationName)}</title>
      <AppBar position="fixed" sx={{ zIndex: (t) => t.zIndex.drawer + 1 }}>
        <Toolbar sx={{ gap: 1 }}>
          {!isDesktop && (
            <IconButton
              color="inherit"
              edge="start"
              aria-label={strings.shell.openNavigation}
              onClick={() => setMobileOpen(true)}
            >
              <MenuIcon />
            </IconButton>
          )}
          <Typography variant="h6" component="div" noWrap sx={{ display: { xs: 'none', sm: 'block' }, mr: 2 }}>
            {strings.app.name}
          </Typography>
          <OrganizationSwitcher />
          <Box sx={{ flexGrow: 1 }} />
          <UserMenu />
        </Toolbar>
        <NavigationProgress />
      </AppBar>

      <Drawer
        variant={isDesktop ? 'permanent' : 'temporary'}
        open={isDesktop || mobileOpen}
        onClose={() => setMobileOpen(false)}
        sx={{
          ...(isDesktop && { width: DRAWER_WIDTH, flexShrink: 0 }),
          '& .MuiDrawer-paper': { width: DRAWER_WIDTH, boxSizing: 'border-box' },
        }}
      >
        <Toolbar />
        <List component="nav" aria-label={strings.shell.navigation}>
          {visibleNavItems(roles).map(({ to, label, Icon }) => (
            <ListItemButton
              key={to}
              component={NavLink}
              to={to}
              end
              onClick={() => setMobileOpen(false)}
              sx={{ '&.active': { bgcolor: 'action.selected' } }}
            >
              <ListItemIcon>
                <Icon />
              </ListItemIcon>
              <ListItemText primary={label} />
            </ListItemButton>
          ))}
        </List>
      </Drawer>

      <Box component="main" sx={{ flexGrow: 1, minWidth: 0, p: { xs: 2, md: 3 } }}>
        <Toolbar />
        <Outlet />
      </Box>
    </Box>
  );
}
