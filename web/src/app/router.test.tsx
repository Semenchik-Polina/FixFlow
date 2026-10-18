import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { renderRoute } from '../test/renderWithProviders';

describe('routes', () => {
  it('shows the login page at /login', async () => {
    renderRoute('/login');

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
  });

  it('sends / to the login page', async () => {
    const { router } = renderRoute('/');

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/login');
  });

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
