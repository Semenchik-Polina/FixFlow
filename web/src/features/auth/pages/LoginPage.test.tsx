import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http as mock } from 'msw';
import { describe, expect, it } from 'vitest';
import { getLogInMockHandler } from '../../../api/generated/fixflow-api/fixflow-api.msw';
import type { LogInCommand } from '../../../api/generated/model';
import { aMembership, givenSignedIn, problem } from '../../../test/fixtures';
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

  it('signs in with what was typed, using the keyboard alone, and opens the only organization', async () => {
    const user = userEvent.setup();
    let sent: LogInCommand | undefined;
    server.use(
      getLogInMockHandler(async ({ request }) => {
        sent = await request.json();
      }),
    );
    givenSignedIn(aMembership('acme'));
    const { router } = renderRoute('/login');

    // The email field has the focus from the start
    await screen.findByRole('heading', { name: 'Sign in' });
    await user.keyboard('pat.kim@example.test');
    await user.tab();
    await user.keyboard('correct horse battery');
    // Past the show-password button, to the checkbox
    await user.tab();
    await user.tab();
    await user.keyboard(' ');
    await user.keyboard('{Enter}');

    expect(await screen.findByRole('heading', { name: 'Home' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/acme');
    expect(sent).toEqual({ email: 'pat.kim@example.test', password: 'correct horse battery', rememberMe: true });
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

  it('disables the button while signing in', async () => {
    const user = userEvent.setup();
    let answer = () => {};
    server.use(getLogInMockHandler(() => new Promise<void>((resolve) => (answer = resolve))));
    givenSignedIn(aMembership('acme'));
    renderRoute('/login');

    await user.type(await screen.findByLabelText('Email'), 'pat.kim@example.test');
    await user.type(screen.getByLabelText('Password'), 'correct horse battery');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    await waitFor(() => expect(screen.getByRole('button', { name: 'Sign in' })).toBeDisabled());
    answer();
    expect(await screen.findByRole('heading', { name: 'Home' })).toBeInTheDocument();
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
