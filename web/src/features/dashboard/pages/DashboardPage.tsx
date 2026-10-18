import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import { dashboardStrings } from '../strings';

// Placeholder organization home until the role-based home pages (plan T1.7, M8)
export function DashboardPage() {
  return (
    <Box>
      <Typography variant="h5" component="h1" gutterBottom>
        {dashboardStrings.home.title}
      </Typography>
      <Typography sx={{ color: 'text.secondary' }}>{dashboardStrings.home.placeholder}</Typography>
    </Box>
  );
}
