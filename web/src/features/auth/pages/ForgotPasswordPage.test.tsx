import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { getForgotPasswordMockHandler } from '../../../api/generated/fixflow-api/fixflow-api.msw';
import type { ForgotPasswordCommand } from '../../../api/generated/model';
import { renderRoute } from '../../../test/renderWithProviders';
import { server } from '../../../test/server';

describe('ForgotPasswordPage', () => {
  it('checks the email before calling the API', async () => {
    const user = userEvent.setup();
    renderRoute('/forgot-password');

    await user.type(await screen.findByLabelText('Email'), 'pat.kim');
    await user.click(screen.getByRole('button', { name: 'Send reset link' }));

    expect(screen.getByLabelText('Email')).toHaveAccessibleDescription('Enter an email address like name@example.com.');
  });

  it('sends the link, without telling whether the email has an account', async () => {
    const user = userEvent.setup();
    let sent: ForgotPasswordCommand | undefined;
    server.use(
      getForgotPasswordMockHandler(async ({ request }) => {
        sent = await request.json();
      }),
    );
    renderRoute('/forgot-password');

    await user.type(await screen.findByLabelText('Email'), ' pat.kim@example.test ');
    await user.click(screen.getByRole('button', { name: 'Send reset link' }));

    expect(await screen.findByRole('heading', { name: 'Check your email' })).toBeInTheDocument();
    expect(screen.getByText(/If pat\.kim@example\.test has a FixFlow account/)).toBeInTheDocument();
    expect(sent).toEqual({ email: 'pat.kim@example.test' });
    expect(screen.getByRole('link', { name: 'Back to sign in' })).toHaveAttribute('href', '/login');
  });

  it('can send the link again', async () => {
    const user = userEvent.setup();
    server.use(getForgotPasswordMockHandler());
    renderRoute('/forgot-password');
    await user.type(await screen.findByLabelText('Email'), 'pat.kim@example.test');
    await user.click(screen.getByRole('button', { name: 'Send reset link' }));

    await user.click(await screen.findByRole('button', { name: 'Send the link again' }));

    expect(screen.getByLabelText('Email')).toHaveValue('pat.kim@example.test');
  });
});
