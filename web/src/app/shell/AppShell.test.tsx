import { fireEvent, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { renderRoute } from '../../test/renderWithProviders';

// jsdom has no matchMedia, so the shell renders its mobile layout here
describe('AppShell on a small screen', () => {
  it('opens the navigation from the menu button', async () => {
    renderRoute('/acme');
    await screen.findByRole('heading', { name: 'Home' });
    expect(screen.queryByRole('navigation', { name: 'Main navigation' })).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Open navigation' }));

    const navigation = await screen.findByRole('navigation', { name: 'Main navigation' });
    expect(within(navigation).getByRole('link', { name: 'Home' })).toHaveAttribute('href', '/acme');
  });
});
