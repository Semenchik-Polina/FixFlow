import CssBaseline from '@mui/material/CssBaseline';
import { ThemeProvider } from '@mui/material/styles';
import { QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import type { ReactElement } from 'react';
import { createMemoryRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { createQueryClient } from '../app/queryClient';
import { createRoutes } from '../app/router';
import { theme } from '../app/theme';

/** Renders with the app's theme and a fresh query client that doesn't retry, so failures show up at once. */
export function renderWithProviders(ui: ReactElement) {
  const queryClient = createQueryClient();
  queryClient.setDefaultOptions({ queries: { ...queryClient.getDefaultOptions().queries, retry: false } });

  return {
    queryClient,
    ...render(
      <ThemeProvider theme={theme}>
        <CssBaseline />
        <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>
      </ThemeProvider>,
    ),
  };
}

/** Renders the app's routes at `path`, with the providers. */
export function renderRoute(path: string) {
  const router = createMemoryRouter(createRoutes(), { initialEntries: [path] });
  return { router, ...renderWithProviders(<RouterProvider router={router} />) };
}
