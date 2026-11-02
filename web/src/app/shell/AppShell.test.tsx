import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { getLogOutMockHandler } from '../../api/generated/fixflow-api/fixflow-api.msw';
import { redirectToLogin } from '../../api/navigation';
import { aMembership, givenSignedIn } from '../../test/fixtures';
import { renderRoute } from '../../test/renderWithProviders';
import { server } from '../../test/server';

vi.mock('../../api/navigation', () => ({ redirectToLogin: vi.fn() }));

// jsdom has no matchMedia, so the shell renders its mobile layout here
describe('AppShell on a small screen', () => {
  it('opens the navigation from the menu button', async () => {
    const user = userEvent.setup();
    givenSignedIn(aMembership('acme'));
    renderRoute('/acme');
    await screen.findByRole('heading', { name: 'Home' });
    expect(screen.queryByRole('navigation', { name: 'Main navigation' })).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Open navigation' }));

    const navigation = await screen.findByRole('navigation', { name: 'Main navigation' });
    expect(within(navigation).getByRole('link', { name: 'Home' })).toHaveAttribute('href', '/acme');
  });
});

describe('AppShell organization switcher', () => {
  it('lists the organizations with the roles held in each, and opens another', async () => {
    const user = userEvent.setup();
    givenSignedIn(aMembership('acme', ['Manager']), aMembership('brightway-homes', ['Technician']));
    const { router } = renderRoute('/acme');

    await user.click(await screen.findByRole('button', { name: 'Acme, switch organization' }));
    const menu = screen.getByRole('menu', { name: 'Organizations' });
    expect(within(menu).getByRole('menuitem', { name: /Acme/ })).toHaveTextContent('Manager');
    await user.click(within(menu).getByRole('menuitem', { name: /Brightway Homes/ }));

    expect(await screen.findByRole('button', { name: 'Brightway Homes, switch organization' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/brightway-homes');
  });

  it('leads to all organizations and to creating one', async () => {
    const user = userEvent.setup();
    givenSignedIn(aMembership('acme'));
    renderRoute('/acme');

    await user.click(await screen.findByRole('button', { name: 'Acme, switch organization' }));

    expect(screen.getByRole('menuitem', { name: 'All organizations' })).toHaveAttribute('href', '/organizations');
    expect(screen.getByRole('menuitem', { name: 'Create organization' })).toHaveAttribute('href', '/organizations/new');
  });
});

describe('AppShell user menu', () => {
  it('shows who is signed in, and signs out', async () => {
    const user = userEvent.setup();
    const signedOut = vi.fn();
    server.use(getLogOutMockHandler(signedOut));
    givenSignedIn(aMembership('acme'));
    renderRoute('/acme');

    await user.click(await screen.findByRole('button', { name: 'Account' }));
    const menu = screen.getByRole('menu');
    expect(menu).toHaveTextContent('Pat Kim');
    expect(menu).toHaveTextContent('pat.kim@example.test');
    await user.click(within(menu).getByRole('menuitem', { name: 'Sign out' }));

    await vi.waitFor(() => expect(redirectToLogin).toHaveBeenCalledOnce());
    expect(signedOut).toHaveBeenCalledOnce();
  });
});
