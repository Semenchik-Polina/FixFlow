import Container from '@mui/material/Container';
import Typography from '@mui/material/Typography';
import { authStrings } from '../strings';

// Placeholder until the real sign-in page (plan T1.7)
export function LoginPage() {
  return (
    <Container maxWidth="xs" sx={{ py: 8 }}>
      <Typography variant="h4" component="h1" gutterBottom>
        {authStrings.login.title}
      </Typography>
      <Typography sx={{ color: 'text.secondary' }}>{authStrings.login.placeholder}</Typography>
    </Container>
  );
}
