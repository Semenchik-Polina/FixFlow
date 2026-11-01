import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http as mock } from 'msw';
import { describe, expect, it } from 'vitest';
import { problem } from '../../../test/fixtures';
import { renderRoute } from '../../../test/renderWithProviders';
import { server } from '../../../test/server';

const FAILURE =
  'The email or password is incorrect, or the account is locked for a while after too many failed attempts.';

describe('LoginPage', () => {
  it('asks for the email and password before calling the API', async () => {
    const user = userEvent.setup();
    renderRoute('/login');

    await user.click(await screen.findByRole('button', { name: 'Sign in' }));

    expect(screen.getByLabelText('Email')).toHaveAccessibleDescription('Enter your email.');
    expect(screen.getByLabelText('Password')).toHaveAccessibleDescription('Enter your password.');
    expect(screen.getByLabelText('Email')).toHaveFocus();
  });

  it("shows the API's reason when the sign-in fails, and stays", async () => {
    const user = userEvent.setup();
    server.use(mock.post('*/api/v1/auth/login', () => problem(422, { detail: FAILURE })));
    const { router } = renderRoute('/login');

    await user.type(await screen.findByLabelText('Email'), 'pat.kim@example.test');
    await user.type(screen.getByLabelText('Password'), 'wrong password');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(FAILURE);
    expect(router.state.location.pathname).toBe('/login');
    expect(screen.getByLabelText('Email')).toHaveValue('pat.kim@example.test');
  });

  it('asks to wait after too many attempts', async () => {
    const user = userEvent.setup();
    server.use(mock.post('*/api/v1/auth/login', () => problem(429, { title: 'Too Many Requests' })));
    renderRoute('/login');

    await user.type(await screen.findByLabelText('Email'), 'pat.kim@example.test');
    await user.type(screen.getByLabelText('Password'), 'some password');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Too many attempts. Wait a minute, then try again.');
  });

  it('leads to password reset and signup', async () => {
    renderRoute('/login');

    expect(await screen.findByRole('link', { name: 'Forgot your password?' })).toHaveAttribute(
      'href',
      '/forgot-password',
    );
    expect(screen.getByRole('link', { name: 'Create an account' })).toHaveAttribute('href', '/signup');
  });
});
