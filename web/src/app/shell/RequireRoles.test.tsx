import { screen } from '@testing-library/react';
import { createMemoryRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { describe, expect, it } from 'vitest';
import type { Role } from '../../api/generated/model';
import { aMembership, givenSignedIn } from '../../test/fixtures';
import { renderWithProviders } from '../../test/renderWithProviders';
import { OrganizationLayout } from './OrganizationLayout';
import { RequireRoles } from './RequireRoles';

function renderManagersPage(roles: Role[]) {
  givenSignedIn(aMembership('acme', roles));
  const router = createMemoryRouter(
    [
      {
        path: ':orgSlug',
        Component: OrganizationLayout,
        children: [
          {
            element: <RequireRoles roles={['Manager', 'Admin']} />,
            children: [{ path: 'properties', element: <h1>Properties</h1> }],
          },
        ],
      },
    ],
    { initialEntries: ['/acme/properties'] },
  );
  renderWithProviders(<RouterProvider router={router} />);
}

describe('RequireRoles', () => {
  it.each<[Role[]]>([[['Manager']], [['Admin']], [['Technician', 'Manager']]])(
    'opens the page for %j',
    async (roles) => {
      renderManagersPage(roles);

      expect(await screen.findByRole('heading', { name: 'Properties' })).toBeInTheDocument();
    },
  );

  it.each<[Role[]]>([[['Tenant']], [['Technician']]])('shows "Page not found" to %j', async (roles) => {
    renderManagersPage(roles);

    expect(await screen.findByRole('heading', { name: 'Page not found' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Properties' })).not.toBeInTheDocument();
  });
});
