import { screen, waitFor, within } from '@testing-library/react';
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
    givenSignedIn(aMembership('acme'));

    renderRoute('/acme');

    expect(await screen.findByRole('heading', { name: 'Home' })).toBeInTheDocument();
    const banner = screen.getByRole('banner');
    expect(banner).toHaveTextContent('FixFlow');
    expect(within(banner).getByRole('button', { name: 'Acme, switch organization' })).toBeInTheDocument();
    await waitFor(() => expect(document.title).toBe('Acme – FixFlow'));
  });

  it('shows "Page not found" for an unknown page of an organization', async () => {
    givenSignedIn(aMembership('acme'));

    renderRoute('/acme/nope');

    expect(await screen.findByRole('heading', { name: 'Page not found' })).toBeInTheDocument();
    expect(screen.getByRole('banner')).toHaveTextContent('FixFlow');
  });

  it('shows "Organization not found" for an organization the user is no member of', async () => {
    givenSignedIn(aMembership('acme'));

    renderRoute('/brightway-homes');

    expect(await screen.findByRole('heading', { name: 'Organization not found' })).toBeInTheDocument();
    expect(screen.getByText(/no organization at \/brightway-homes that you can open/)).toBeInTheDocument();
    expect(screen.queryByRole('banner')).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Your organizations' })).toHaveAttribute('href', '/organizations');
  });

  it('doesn\'t repeat a crafted address on the "Organization not found" page', async () => {
    givenSignedIn(aMembership('acme'));

    renderRoute('/Your%20account%20is%20suspended.%20Call%20555-0100');

    expect(await screen.findByRole('heading', { name: 'Organization not found' })).toBeInTheDocument();
    expect(screen.getByText(/no organization at this address that you can open/)).toBeInTheDocument();
    expect(document.body).not.toHaveTextContent('suspended');
  });

  it('offers to try again when the session fails to load', async () => {
    server.use(mock.get('*/api/v1/me', () => problem(503)));

    renderRoute('/acme');

    expect(await screen.findByRole('alert')).toHaveTextContent('Something went wrong on our side');
    givenSignedIn(aMembership('acme'));
    screen.getByRole('button', { name: 'Try again' }).click();
    expect(await screen.findByRole('heading', { name: 'Home' })).toBeInTheDocument();
  });
});
