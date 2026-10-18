import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import { isRouteErrorResponse, useRouteError } from 'react-router';
import { strings } from '../strings';
import { NotFoundPage } from './NotFoundPage';

/** The router's error boundary: an unknown route, a page that failed to load, or a render error. */
export function RouteErrorPage() {
  const error = useRouteError();
  if (isRouteErrorResponse(error) && error.status === 404) {
    return (
      <Box sx={{ px: 2, py: 4 }}>
        <NotFoundPage />
      </Box>
    );
  }

  return (
    <Box sx={{ px: 2, py: 4 }}>
      <Typography variant="h5" component="h1" gutterBottom>
        {strings.errors.generic}
      </Typography>
      <Typography sx={{ color: 'text.secondary', mb: 2 }}>{strings.errors.genericDetail}</Typography>
      <Button variant="contained" onClick={() => window.location.reload()}>
        {strings.common.reload}
      </Button>
    </Box>
  );
}
