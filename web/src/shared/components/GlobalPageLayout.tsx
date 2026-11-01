import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import type { ReactNode } from 'react';
import { strings } from '../strings';

interface GlobalPageLayoutProps {
  /** The page's heading, and the browser tab's title. */
  title: string;
  children: ReactNode;
}

/**
 * The frame of a page outside any organization (sign in, the organization picker): the wordmark, the title and one
 * narrow column, with no card around it.
 */
export function GlobalPageLayout({ title, children }: GlobalPageLayoutProps) {
  return (
    <Box component="main" sx={{ px: 2, py: { xs: 4, sm: 8 } }}>
      <title>{strings.pageTitle(title)}</title>
      <Box sx={{ maxWidth: 400, mx: 'auto' }}>
        <Typography variant="h6" component="p" sx={{ color: 'primary.main', fontWeight: 700, mb: { xs: 4, sm: 6 } }}>
          {strings.app.name}
        </Typography>
        <Typography variant="h1" sx={{ mb: 3 }}>
          {title}
        </Typography>
        {children}
      </Box>
    </Box>
  );
}
