import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http as mock } from 'msw';
import { beforeAll, describe, expect, it } from 'vitest';
import {
  getCreatePropertyMockHandler,
  getGetPropertyMockHandler,
  getListPropertiesMockHandler,
  getListUnitsMockHandler,
} from '../../../api/generated/fixflow-api/fixflow-api.msw';
import type { Role } from '../../../api/generated/model';
import { aMembership, aPage, aProperty, givenPropertyTimeZones, givenSignedIn, problem } from '../../../test/fixtures';
import { renderRoute } from '../../../test/renderWithProviders';
import { server } from '../../../test/server';

// The router loads the pages lazily, and the first load of their code (with the DataGrid) can outlast a 1 s wait for
// the page. Loaded once here, it stays out of the tests' waits.
beforeAll(async () => {
  await Promise.all([import('./PropertiesPage'), import('./PropertyDetailPage')]);
});

const riverside = aProperty();
const harbour = aProperty({ id: '0199b1c2-0000-7000-8000-000000000302', name: 'Harbour View' });

/** Answers the list with `riverside` on page 1 and `harbour` on page 2, and keeps each request's query string. */
function givenTwoPages(): URLSearchParams[] {
  const requests: URLSearchParams[] = [];
  server.use(
    getListPropertiesMockHandler(({ request }) => {
      const query = new URL(request.url).searchParams;
      requests.push(query);
      return query.get('page') === '2'
        ? aPage([harbour], { page: 2, totalCount: 26 })
        : aPage([riverside], { totalCount: 26 });
    }),
  );
  return requests;
}

describe('PropertiesPage', () => {
  it('lists the properties a page at a time, each linking to its page', async () => {
    const user = userEvent.setup();
    givenSignedIn(aMembership('acme', ['Manager']));
    const requests = givenTwoPages();
    renderRoute('/acme/properties');

    expect(await screen.findByRole('heading', { level: 1, name: 'Properties' })).toBeInTheDocument();
    expect(await screen.findByRole('link', { name: 'Riverside Apartments' })).toHaveAttribute(
      'href',
      `/acme/properties/${riverside.id}`,
    );
    expect(screen.getByText('1 Test Street, Testville')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /next page/i }));

    expect(await screen.findByRole('link', { name: 'Harbour View' })).toBeInTheDocument();
    expect(requests.map((query) => [query.get('page'), query.get('pageSize')])).toEqual([
      ['1', '25'],
      ['2', '25'],
    ]);
  });

  it('searches by name from the first page', async () => {
    const user = userEvent.setup();
    givenSignedIn(aMembership('acme', ['Admin']));
    const requests = givenTwoPages();
    renderRoute('/acme/properties');
    await screen.findByRole('link', { name: 'Riverside Apartments' });
    await user.click(screen.getByRole('button', { name: /next page/i }));
    await screen.findByRole('link', { name: 'Harbour View' });

    await user.type(screen.getByRole('searchbox', { name: 'Search by name' }), ' river ');

    await waitFor(() => expect(requests.at(-1)?.get('name')).toBe('river'));
    expect(requests.at(-1)?.get('page')).toBe('1');
    // One search when typing pauses, not one per key
    expect(requests.filter((query) => query.has('name'))).toHaveLength(1);
  });

  it('invites adding the first property when there is none', async () => {
    givenSignedIn(aMembership('acme', ['Manager']));
    server.use(getListPropertiesMockHandler(aPage([])));
    renderRoute('/acme/properties');

    expect(
      await screen.findByText('No properties yet. Add your first building with New property.'),
    ).toBeInTheDocument();
  });

  it('offers to try again when the list fails to load', async () => {
    const user = userEvent.setup();
    givenSignedIn(aMembership('acme', ['Manager']));
    givenTwoPages();
    // Added last, so it answers first: a handler added later wins
    server.use(mock.get('*/api/v1/properties', () => problem(503), { once: true }));
    renderRoute('/acme/properties');

    expect(await screen.findByText('Something went wrong on our side. Try again in a moment.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Try again' }));

    expect(await screen.findByRole('link', { name: 'Riverside Apartments' })).toBeInTheDocument();
  });

  it('opens a new property on its own page once created', async () => {
    const user = userEvent.setup();
    givenSignedIn(aMembership('acme', ['Manager']));
    const created = aProperty({ id: '0199b1c2-0000-7000-8000-000000000303', name: 'Elm Court' });
    server.use(
      getListPropertiesMockHandler(aPage([])),
      getCreatePropertyMockHandler(created),
      getGetPropertyMockHandler(created),
      getListUnitsMockHandler(aPage([])),
    );
    givenPropertyTimeZones();
    const { router } = renderRoute('/acme/properties');

    await user.click(await screen.findByRole('button', { name: 'New property' }));
    await user.type(await screen.findByLabelText('Name'), 'Elm Court');
    await user.type(screen.getByLabelText('Street address'), '1 Test Street');
    await user.type(screen.getByLabelText('City'), 'Testville');
    await user.type(screen.getByRole('combobox', { name: 'Country' }), 'Poland');
    await user.click(await screen.findByRole('option', { name: 'Poland (PL)' }));
    await user.click(screen.getByRole('button', { name: 'Create property' }));

    expect(await screen.findByRole('heading', { level: 1, name: 'Elm Court' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe(`/acme/properties/${created.id}`);
  });

  // jsdom has no media queries, so the shell has its small-screen layout: the navigation opens from the menu button

  it.each<Role>(['Technician', 'Tenant'])('shows "Page not found" to a %s, and no navigation to it', async (role) => {
    const user = userEvent.setup();
    givenSignedIn(aMembership('acme', [role]));
    renderRoute('/acme/properties');

    expect(await screen.findByRole('heading', { name: 'Page not found' })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Open navigation' }));
    expect(await screen.findByRole('link', { name: 'Home' })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Properties' })).not.toBeInTheDocument();
  });

  it('is in the navigation of Managers and Admins', async () => {
    const user = userEvent.setup();
    givenSignedIn(aMembership('acme', ['Admin']));
    server.use(getListPropertiesMockHandler(aPage([])));
    renderRoute('/acme/properties');

    await user.click(await screen.findByRole('button', { name: 'Open navigation' }));

    expect(await screen.findByRole('link', { name: 'Properties' })).toHaveAttribute('href', '/acme/properties');
  });
});
