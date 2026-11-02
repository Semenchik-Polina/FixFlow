import { screen } from '@testing-library/react';
import userEvent, { type UserEvent } from '@testing-library/user-event';
import { http as mock } from 'msw';
import { describe, expect, it } from 'vitest';
import { getSignUpMockHandler } from '../../../api/generated/fixflow-api/fixflow-api.msw';
import type { SignUpCommand } from '../../../api/generated/model';
import { aMembership, givenSignedIn, validationProblem } from '../../../test/fixtures';
import { renderRoute } from '../../../test/renderWithProviders';
import { server } from '../../../test/server';

async function fillIn(user: UserEvent, organizationName = 'Brightway Homes') {
  await user.type(await screen.findByLabelText('First name'), 'Pat');
  await user.type(screen.getByLabelText('Last name'), 'Kim');
  await user.type(screen.getByLabelText('Email'), 'pat.kim@example.test');
  await user.type(screen.getByLabelText('Password'), 'correct horse battery');
  await user.type(screen.getByLabelText('Organization name'), organizationName);
}

describe('SignupPage', () => {
  it('suggests the web address from the organization name until it is edited', async () => {
    const user = userEvent.setup();
    renderRoute('/signup');

    await user.type(await screen.findByLabelText('Organization name'), 'Café Zürich');
    const slug = screen.getByLabelText('Web address');
    expect(slug).toHaveValue('cafe-zurich');
    expect(slug).toHaveAccessibleDescription(/cafe-zurich\. It can't be changed later\./);

    await user.clear(slug);
    await user.type(slug, 'zurich');
    await user.type(screen.getByLabelText('Organization name'), ' Lofts');
    expect(slug).toHaveValue('zurich');
  });

  it('checks every field before calling the API', async () => {
    const user = userEvent.setup();
    renderRoute('/signup');

    await user.type(await screen.findByLabelText('Password'), 'too short');
    await user.type(screen.getByLabelText('Organization name'), 'Ab');
    await user.click(screen.getByRole('button', { name: 'Create account' }));

    expect(screen.getByLabelText('First name')).toHaveAccessibleDescription('Enter your first name.');
    expect(screen.getByLabelText('Email')).toHaveAccessibleDescription('Enter your email.');
    expect(screen.getByLabelText('Password')).toHaveAccessibleDescription('Use at least 10 characters.');
    expect(screen.getByLabelText('Web address')).toHaveAccessibleDescription('Use 3 to 40 characters.');
    expect(screen.getByLabelText('First name')).toHaveFocus();
  });

  it("shows the API's field errors on their fields, and focuses the first", async () => {
    const user = userEvent.setup();
    server.use(
      mock.post('*/api/v1/auth/signup', () =>
        validationProblem(
          { email: ['An account with this email already exists.'], slug: ['This slug is already taken.'] },
          422,
        ),
      ),
    );
    renderRoute('/signup');

    await fillIn(user);
    await user.click(screen.getByRole('button', { name: 'Create account' }));

    expect(await screen.findByText('This slug is already taken.')).toBeInTheDocument();
    expect(screen.getByLabelText('Web address')).toHaveAccessibleDescription('This slug is already taken.');
    expect(screen.getByLabelText('Email')).toHaveAccessibleDescription('An account with this email already exists.');
    expect(screen.getByLabelText('Email')).toHaveFocus();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('shows a field error the server reports for a reserved slug', async () => {
    const user = userEvent.setup();
    server.use(mock.post('*/api/v1/auth/signup', () => validationProblem({ slug: ['This slug is reserved.'] })));
    renderRoute('/signup');

    await fillIn(user, 'Login');
    await user.click(screen.getByRole('button', { name: 'Create account' }));

    expect(await screen.findByText('This slug is reserved.')).toBeInTheDocument();
    expect(screen.getByLabelText('Web address')).toHaveFocus();
  });

  it('creates the account and opens the new organization', async () => {
    const user = userEvent.setup();
    let sent: SignUpCommand | undefined;
    server.use(
      getSignUpMockHandler(async ({ request }) => {
        sent = await request.json();
        return { id: '0199b1c2-0000-7000-8000-000000000101', name: 'Brightway Homes', slug: 'brightway-homes' };
      }),
    );
    givenSignedIn(aMembership('brightway-homes', ['Admin']));
    const { router } = renderRoute('/signup');

    await fillIn(user);
    await user.click(screen.getByRole('button', { name: 'Create account' }));

    expect(await screen.findByRole('heading', { name: 'Home' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/brightway-homes');
    expect(sent).toEqual({
      firstName: 'Pat',
      lastName: 'Kim',
      email: 'pat.kim@example.test',
      password: 'correct horse battery',
      organizationName: 'Brightway Homes',
      slug: 'brightway-homes',
    });
  });
});
