import CssBaseline from '@mui/material/CssBaseline';
import { ThemeProvider } from '@mui/material/styles';
import { QueryClientProvider, type QueryClient } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import type { ReactElement } from 'react';
import { createMemoryRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { createQueryClient } from '../app/queryClient';
import { createRoutes } from '../app/router';
import { theme } from '../app/theme';
import { GlobalQueryClientContext } from '../shared/session/globalQueryClient';

/** A fresh query client that doesn't retry, so failures show up at once. Organization clients copy its defaults. */
export function createTestQueryClient(): QueryClient {
  const queryClient = createQueryClient();
  queryClient.setDefaultOptions({ queries: { ...queryClient.getDefaultOptions().queries, retry: false } });
  return queryClient;
}

/** Renders with the app's theme and providers, around `queryClient` (a fresh test client by default). */
export function renderWithProviders(ui: ReactElement, queryClient: QueryClient = createTestQueryClient()) {
  return {
    queryClient,
    ...render(
      <ThemeProvider theme={theme}>
        <CssBaseline />
        <QueryClientProvider client={queryClient}>
          <GlobalQueryClientContext value={queryClient}>{ui}</GlobalQueryClientContext>
        </QueryClientProvider>
      </ThemeProvider>,
    ),
  };
}

/** Renders the app's routes at `path`, with the providers. */
export function renderRoute(path: string) {
  const router = createMemoryRouter(createRoutes(), { initialEntries: [path] });
  return { router, ...renderWithProviders(<RouterProvider router={router} />) };
}
