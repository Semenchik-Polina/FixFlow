import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { delay, http as mock, HttpResponse } from 'msw';
import { StrictMode } from 'react';
import { createMemoryRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { beforeEach, describe, expect, it } from 'vitest';
import { getSignUpMockHandler } from '../../api/generated/fixflow-api/fixflow-api.msw';
import { createRoutes } from '../../app/router';
import { aMembership, aUser } from '../../test/fixtures';
import { createTestQueryClient, renderWithProviders } from '../../test/renderWithProviders';
import { server } from '../../test/server';

// The abort signal of every /me request, to see which ones the SPA cancelled
let signals: AbortSignal[];

beforeEach(() => {
  signals = [];
  server.use(
    mock.get('*/api/v1/me', async ({ request }) => {
      signals.push(request.signal);
      // Still in flight while the next page takes over
      await delay(30);
      return HttpResponse.json(aUser(aMembership('acme')));
    }),
  );
});

function renderApp(path: string, strict: boolean) {
  const queryClient = createTestQueryClient();
  const router = createMemoryRouter(createRoutes(queryClient), { initialEntries: [path] });
  const app = <RouterProvider router={router} />;
  renderWithProviders(strict ? <StrictMode>{app}</StrictMode> : app, queryClient);
  return router;
}

describe('useCurrentUser', () => {
  it('lets the session read finish when React remounts the page (StrictMode, as in development)', async () => {
    renderApp('/acme', true);

    expect(await screen.findByRole('heading', { name: 'Home' })).toBeInTheDocument();
    expect(signals.filter((signal) => signal.aborted)).toHaveLength(0);
  });

  it('lets the session read finish after signup (StrictMode, as in development)', async () => {
    const user = userEvent.setup();
    server.use(getSignUpMockHandler({ id: '0199b1c2-0000-7000-8000-000000000101', name: 'Acme', slug: 'acme' }));
    renderApp('/signup', true);

    await user.type(await screen.findByLabelText('First name'), 'Pat');
    await user.type(screen.getByLabelText('Last name'), 'Kim');
    await user.type(screen.getByLabelText('Email'), 'pat.kim@example.test');
    await user.type(screen.getByLabelText('Password'), 'correct horse battery');
    await user.type(screen.getByLabelText('Organization name'), 'Acme');
    await user.click(screen.getByRole('button', { name: 'Create account' }));

    expect(await screen.findByRole('heading', { name: 'Home' })).toBeInTheDocument();
    expect(signals.filter((signal) => signal.aborted)).toHaveLength(0);
  });

  it('lets the session read finish when the page that read it hands over to the next', async () => {
    const router = renderApp('/', false);

    expect(await screen.findByRole('heading', { name: 'Home' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/acme');
    expect(signals.filter((signal) => signal.aborted)).toHaveLength(0);
  });
});
