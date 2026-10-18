import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import { strings } from '../strings';

export function NotFoundPage() {
  return (
    <Box>
      <Typography variant="h5" component="h1" gutterBottom>
        {strings.errors.notFound}
      </Typography>
      <Typography sx={{ color: 'text.secondary' }}>{strings.errors.notFoundDetail}</Typography>
    </Box>
  );
}
