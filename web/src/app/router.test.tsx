import { screen, waitFor } from '@testing-library/react';
import { http as mock } from 'msw';
import { describe, expect, it, vi } from 'vitest';
import { redirectToLogin } from '../api/navigation';
import { aMembership, givenSignedIn, problem } from '../test/fixtures';
import { renderRoute } from '../test/renderWithProviders';
import { server } from '../test/server';

vi.mock('../api/navigation', () => ({ redirectToLogin: vi.fn() }));

describe('routes: global pages', () => {
  it.each([
    ['/login', 'Sign in'],
    ['/signup', 'Create your account'],
    ['/forgot-password', 'Reset your password'],
    ['/reset-password', 'This link is incomplete'],
  ])('shows %s to a signed-out visitor', async (path, heading) => {
    renderRoute(path);

    expect(await screen.findByRole('heading', { level: 1, name: heading })).toBeInTheDocument();
  });

  it('sends a signed-out visitor at / to the login page', async () => {
    server.use(mock.get('*/api/v1/me', () => problem(401, { title: 'Unauthorized' })));

    renderRoute('/');

    await waitFor(() => expect(redirectToLogin).toHaveBeenCalled());
  });
});

describe('routes: after sign-in', () => {
  it('opens the only organization from /', async () => {
    givenSignedIn(aMembership('acme'));

    const { router } = renderRoute('/');

    expect(await screen.findByRole('heading', { name: 'Home' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/acme');
  });

  it('offers the organizations to choose from / when there are several', async () => {
    givenSignedIn(aMembership('acme', ['Manager']), aMembership('brightway-homes', ['Technician', 'Tenant']));

    const { router } = renderRoute('/');

    expect(await screen.findByRole('heading', { name: 'Your organizations' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/organizations');
    expect(screen.getByRole('link', { name: /Acme/ })).toHaveAttribute('href', '/acme');
    expect(screen.getByRole('link', { name: /Brightway Homes/ })).toHaveTextContent('Technician, Tenant');
  });

  it('asks to create an organization from / when there is none', async () => {
    givenSignedIn();

    const { router } = renderRoute('/');

    expect(await screen.findByRole('heading', { name: 'Create an organization' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/organizations/new');
  });
});

describe('routes: organization pages', () => {
  it('shows the organization home inside the app shell', async () => {
    renderRoute('/acme');

    expect(await screen.findByRole('heading', { name: 'Home' })).toBeInTheDocument();
    expect(screen.getByRole('banner')).toHaveTextContent('FixFlow');
    expect(screen.getByRole('button', { name: 'Open navigation' })).toBeInTheDocument();
  });

  it('shows "Page not found" for an unknown organization page', async () => {
    renderRoute('/acme/nope');

    expect(await screen.findByRole('heading', { name: 'Page not found' })).toBeInTheDocument();
    expect(screen.getByRole('banner')).toHaveTextContent('FixFlow');
  });
});
