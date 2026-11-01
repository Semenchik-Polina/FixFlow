import { createTheme } from '@mui/material/styles';

// Light only: a dark theme is open question Q14 (Phase 3). Colours are defined here and nowhere else, and
// components use theme tokens, so a dark palette can be added later without touching them.
export const theme = createTheme({
  palette: {
    mode: 'light',
    primary: { main: '#1f5fa8' },
    secondary: { main: '#00796b' },
    background: { default: '#f5f7fa' },
  },
  typography: {
    // System fonts: nothing to download, no third-party font request
    fontFamily: ['system-ui', '-apple-system', '"Segoe UI"', 'Roboto', '"Helvetica Neue"', 'Arial', 'sans-serif'].join(
      ',',
    ),
    // The title of a global page (sign in, the organization picker): MUI's default h1 is a 6rem display size
    h1: { fontSize: '1.75rem', fontWeight: 600, lineHeight: 1.25, letterSpacing: '-0.01em' },
    // Sentence case everywhere: "Sign in", not "SIGN IN"
    button: { textTransform: 'none', fontWeight: 600 },
  },
  shape: {
    borderRadius: 8,
  },
});
