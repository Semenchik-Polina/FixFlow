import CssBaseline from '@mui/material/CssBaseline';
import { ThemeProvider } from '@mui/material/styles';
import { QueryClientProvider } from '@tanstack/react-query';
import { RouterProvider } from 'react-router/dom';
import { GlobalQueryClientContext } from '../shared/session/globalQueryClient';
import { createQueryClient } from './queryClient';
import { createAppRouter } from './router';
import { theme } from './theme';

const queryClient = createQueryClient();
const router = createAppRouter(queryClient);

export function App() {
  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <QueryClientProvider client={queryClient}>
        <GlobalQueryClientContext value={queryClient}>
          <RouterProvider router={router} />
        </GlobalQueryClientContext>
      </QueryClientProvider>
    </ThemeProvider>
  );
}
