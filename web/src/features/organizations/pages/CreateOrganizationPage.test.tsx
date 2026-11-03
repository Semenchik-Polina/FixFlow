import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http as mock } from 'msw';
import { describe, expect, it } from 'vitest';
import {
  getCreateOrganizationMockHandler,
  getGetMeMockHandler,
} from '../../../api/generated/fixflow-api/fixflow-api.msw';
import type { CreateOrganizationCommand } from '../../../api/generated/model';
import { aMembership, aUser, givenSignedIn, validationProblem } from '../../../test/fixtures';
import { renderRoute } from '../../../test/renderWithProviders';
import { server } from '../../../test/server';

describe('CreateOrganizationPage', () => {
  it('creates the organization and opens it, with the new membership in the session', async () => {
    const user = userEvent.setup();
    const memberships = [aMembership('acme')];
    let sent: CreateOrganizationCommand | undefined;
    server.use(
      getGetMeMockHandler(() => aUser(...memberships)),
      getCreateOrganizationMockHandler(async ({ request }) => {
        sent = await request.json();
        memberships.push(aMembership('riverside-lofts', ['Admin']));
        return { id: '0199b1c2-0000-7000-8000-000000000201', name: 'Riverside Lofts', slug: 'riverside-lofts' };
      }),
    );
    const { router } = renderRoute('/organizations/new');

    expect(await screen.findByText('Signed in as pat.kim@example.test.')).toBeInTheDocument();
    await user.type(screen.getByLabelText('Organization name'), 'Riverside Lofts');
    await user.click(screen.getByRole('button', { name: 'Create organization' }));

    // The session read before creating it doesn't have the organization: the layout must not use it
    expect(await screen.findByRole('button', { name: 'Riverside Lofts, switch organization' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/riverside-lofts');
    expect(sent).toEqual({ name: 'Riverside Lofts', slug: 'riverside-lofts' });
  });

  it('opens the organization at the slug the API stored, not the one typed', async () => {
    const user = userEvent.setup();
    const memberships = [aMembership('acme')];
    server.use(
      getGetMeMockHandler(() => aUser(...memberships)),
      getCreateOrganizationMockHandler(() => {
        memberships.push(aMembership('riverside-lofts-2', ['Admin']));
        return { id: '0199b1c2-0000-7000-8000-000000000202', name: 'Riverside Lofts', slug: 'riverside-lofts-2' };
      }),
    );
    const { router } = renderRoute('/organizations/new');

    await user.type(await screen.findByLabelText('Organization name'), 'Riverside Lofts');
    await user.click(screen.getByRole('button', { name: 'Create organization' }));

    expect(await screen.findByRole('button', { name: 'Riverside Lofts 2, switch organization' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/riverside-lofts-2');
  });

  it("shows the API's error on the organization name field", async () => {
    const user = userEvent.setup();
    givenSignedIn(aMembership('acme'));
    server.use(
      mock.post('*/api/v1/organizations', () =>
        validationProblem({ name: ["'Name' must not contain control characters."] }),
      ),
    );
    renderRoute('/organizations/new');

    await user.type(await screen.findByLabelText('Organization name'), 'Riverside Lofts');
    await user.click(screen.getByRole('button', { name: 'Create organization' }));

    expect(await screen.findByText("'Name' must not contain control characters.")).toBeInTheDocument();
    expect(screen.getByLabelText('Organization name')).toHaveFocus();
  });
});
