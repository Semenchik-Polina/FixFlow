import HomeOutlined from '@mui/icons-material/HomeOutlined';
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
import { strings } from '../../shared/strings';
import { NavigationProgress } from './NavigationProgress';

const DRAWER_WIDTH = 240;

// Relative to the organization (/:orgSlug). Role-based items come with the auth shell (plan T1.7).
const navItems = [{ to: '.', label: strings.shell.home, Icon: HomeOutlined }];

/**
 * The layout of every organization page: app bar, navigation and the page. Mobile-first: below `md` the navigation
 * is a drawer behind a menu button.
 */
export function AppShell() {
  const theme = useTheme();
  const isDesktop = useMediaQuery(theme.breakpoints.up('md'), { noSsr: true });
  const [mobileOpen, setMobileOpen] = useState(false);

  return (
    <Box sx={{ display: 'flex', minHeight: '100dvh' }}>
      <AppBar position="fixed" sx={{ zIndex: (t) => t.zIndex.drawer + 1 }}>
        <Toolbar>
          {!isDesktop && (
            <IconButton
              color="inherit"
              edge="start"
              aria-label={strings.shell.openNavigation}
              onClick={() => setMobileOpen(true)}
              sx={{ mr: 1 }}
            >
              <MenuIcon />
            </IconButton>
          )}
          <Typography variant="h6" component="div" noWrap>
            {strings.app.name}
          </Typography>
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
          {navItems.map(({ to, label, Icon }) => (
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
