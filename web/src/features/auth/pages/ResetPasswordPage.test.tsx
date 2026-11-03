import { screen } from '@testing-library/react';
import userEvent, { type UserEvent } from '@testing-library/user-event';
import { http as mock } from 'msw';
import { describe, expect, it } from 'vitest';
import { getResetPasswordMockHandler } from '../../../api/generated/fixflow-api/fixflow-api.msw';
import type { ResetPasswordCommand } from '../../../api/generated/model';
import { problem, validationProblem } from '../../../test/fixtures';
import { renderRoute } from '../../../test/renderWithProviders';
import { server } from '../../../test/server';

// As the email's link carries it: escaped, in the fragment
const LINK = '/reset-password#token=0199b1c2.CfDJ8%2Bab%2Fcd';
const INVALID_LINK = 'This reset link is invalid or has expired. Ask for a new one.';

async function choose(user: UserEvent, password: string, confirmation = password) {
  await user.type(await screen.findByLabelText('New password'), password);
  await user.type(screen.getByLabelText('Confirm new password'), confirmation);
  await user.click(screen.getByRole('button', { name: 'Set new password' }));
}

describe('ResetPasswordPage', () => {
  it('sets the password with the code from the link, then asks to sign in with it', async () => {
    const user = userEvent.setup();
    let sent: ResetPasswordCommand | undefined;
    server.use(
      getResetPasswordMockHandler(async ({ request }) => {
        sent = await request.json();
      }),
    );
    const { router } = renderRoute(LINK);

    await choose(user, 'a brand new passphrase');

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
    expect(screen.getByRole('alert')).toHaveTextContent('Your password is changed. Sign in with the new one.');
    expect(router.state.location.pathname).toBe('/login');
    expect(sent).toEqual({ token: '0199b1c2.CfDJ8+ab/cd', newPassword: 'a brand new passphrase' });
  });

  it('checks that the two passwords match', async () => {
    const user = userEvent.setup();
    renderRoute(LINK);

    await choose(user, 'a brand new passphrase', 'a brand new passphrase!');

    expect(screen.getByLabelText('Confirm new password')).toHaveAccessibleDescription("The passwords don't match.");
  });

  it('offers a new link when the code is invalid, expired or used', async () => {
    const user = userEvent.setup();
    server.use(mock.post('*/api/v1/auth/reset-password', () => problem(422, { detail: INVALID_LINK })));
    renderRoute(LINK);

    await choose(user, 'a brand new passphrase');

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(INVALID_LINK);
    expect(screen.getByRole('link', { name: 'Ask for a new link' })).toHaveAttribute('href', '/forgot-password');
  });

  it('offers a new link when the API rejects the code itself', async () => {
    const user = userEvent.setup();
    server.use(
      mock.post('*/api/v1/auth/reset-password', () =>
        validationProblem({ token: ["'Token' must be 2048 characters or fewer."] }),
      ),
    );
    renderRoute(LINK);

    await choose(user, 'a brand new passphrase');

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Open the link from the email again, or ask for a new one.',
    );
    expect(screen.getByRole('link', { name: 'Ask for a new link' })).toHaveAttribute('href', '/forgot-password');
  });

  it('asks to set the password again after a conflicting save', async () => {
    const user = userEvent.setup();
    server.use(mock.post('*/api/v1/auth/reset-password', () => problem(409, { title: 'Conflict' })));
    renderRoute(LINK);

    await choose(user, 'a brand new passphrase');

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Your account changed while saving. Set your new password again.',
    );
  });

  it("shows the API's field error on the password", async () => {
    const user = userEvent.setup();
    server.use(
      mock.post('*/api/v1/auth/reset-password', () =>
        validationProblem({ newPassword: ["'New Password' must be between 10 and 128 characters."] }),
      ),
    );
    renderRoute(LINK);

    await choose(user, 'a brand new passphrase');

    expect(await screen.findByText("'New Password' must be between 10 and 128 characters.")).toBeInTheDocument();
    expect(screen.getByLabelText('New password')).toHaveFocus();
  });

  it('explains a link without its code', async () => {
    renderRoute('/reset-password');

    expect(await screen.findByRole('heading', { name: 'This link is incomplete' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Ask for a new link' })).toHaveAttribute('href', '/forgot-password');
  });
});
