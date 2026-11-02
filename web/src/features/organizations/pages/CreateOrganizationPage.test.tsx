import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http as mock } from 'msw';
import { describe, expect, it } from 'vitest';
import { aMembership, givenSignedIn, validationProblem } from '../../../test/fixtures';
import { renderRoute } from '../../../test/renderWithProviders';
import { server } from '../../../test/server';

describe('CreateOrganizationPage', () => {
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
