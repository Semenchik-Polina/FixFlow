import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http as mock } from 'msw';
import { beforeAll, describe, expect, it, vi } from 'vitest';
import {
  getArchivePropertyMockHandler,
  getArchiveUnitMockHandler,
  getGetPropertyMockHandler,
  getListPropertiesMockHandler,
  getListUnitsMockHandler,
} from '../../../api/generated/fixflow-api/fixflow-api.msw';
import type { UnitResponse } from '../../../api/generated/model';
import { aMembership, aPage, aProperty, aUnit, givenSignedIn, problem } from '../../../test/fixtures';
import { renderRoute } from '../../../test/renderWithProviders';
import { server } from '../../../test/server';

// The router loads the pages lazily, and the first load of their code (with the DataGrid) can outlast a 1 s wait for
// the page. Loaded once here, it stays out of the tests' waits.
beforeAll(async () => {
  await Promise.all([import('./PropertiesPage'), import('./PropertyDetailPage')]);
});

const property = aProperty({
  address: { line1: '1 Test Street', line2: 'Building C', city: 'Testville', postalCode: '00-001', countryCode: 'PL' },
});
const path = `/acme/properties/${property.id}`;

/** A Manager opens the property, which has `units`; the list behind them answers an archive's refetch. */
function givenProperty(units: UnitResponse[]) {
  givenSignedIn(aMembership('acme', ['Manager']));
  server.use(
    getGetPropertyMockHandler(property),
    getListUnitsMockHandler(() => aPage(units)),
    getListPropertiesMockHandler(aPage([])),
  );
}

describe('PropertyDetailPage', () => {
  it('shows the address, the time zone and the units', async () => {
    givenProperty([aUnit(property.id, '4A'), aUnit(property.id, '4B')]);
    renderRoute(path);

    expect(await screen.findByRole('heading', { level: 1, name: 'Riverside Apartments' })).toBeInTheDocument();
    for (const line of ['1 Test Street', 'Building C', '00-001 Testville', 'Poland', 'Time zone: Europe/Warsaw']) {
      expect(screen.getByText(line)).toBeInTheDocument();
    }
    const units = screen.getByRole('grid', { name: 'Units' });
    expect(await within(units).findByText('4A')).toBeInTheDocument();
    expect(within(units).getByText('4B')).toBeInTheDocument();
  });

  it('archives the property with its units after asking, and returns to the list', async () => {
    const user = userEvent.setup();
    givenProperty([aUnit(property.id, '4A'), aUnit(property.id, '4B')]);
    const archived = vi.fn();
    server.use(getArchivePropertyMockHandler(({ params }) => void archived(params.id)));
    const { router } = renderRoute(path);
    await screen.findByText('4A');

    await user.click(screen.getByRole('button', { name: 'Archive' }));
    const dialog = screen.getByRole('dialog', { name: 'Archive Riverside Apartments?' });
    expect(dialog).toHaveTextContent('Its 2 units are archived with it.');
    await user.click(within(dialog).getByRole('button', { name: 'Archive property' }));

    expect(await screen.findByRole('heading', { level: 1, name: 'Properties' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/acme/properties');
    expect(archived).toHaveBeenCalledWith(property.id);
  });

  it('keeps the confirmation open with the reason when archiving fails', async () => {
    const user = userEvent.setup();
    givenProperty([]);
    server.use(mock.post('*/api/v1/properties/:id/archive', () => problem(500)));
    const { router } = renderRoute(path);

    await user.click(await screen.findByRole('button', { name: 'Archive' }));
    const dialog = screen.getByRole('dialog', { name: 'Archive Riverside Apartments?' });
    await user.click(within(dialog).getByRole('button', { name: 'Archive property' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent(
      'Something went wrong on our side. Try again in a moment.',
    );
    expect(router.state.location.pathname).toBe(path);
  });

  it('archives a unit after asking, and lists the units again', async () => {
    const user = userEvent.setup();
    const unit = aUnit(property.id, '4A');
    let units = [unit];
    givenProperty([]);
    server.use(
      getListUnitsMockHandler(() => aPage(units)),
      getArchiveUnitMockHandler(({ params }) => {
        expect(params).toMatchObject({ propertyId: property.id, unitId: unit.id });
        units = [];
      }),
    );
    renderRoute(path);

    await user.click(await screen.findByRole('button', { name: 'Archive 4A' }));
    const dialog = screen.getByRole('dialog', { name: 'Archive unit 4A?' });
    await user.click(within(dialog).getByRole('button', { name: 'Archive unit' }));

    expect(
      await screen.findByText('No units yet. Add the apartments, offices or other spaces tenants rent.'),
    ).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('shows "Page not found" for a property it can\'t see', async () => {
    givenSignedIn(aMembership('acme', ['Manager']));
    server.use(
      mock.get('*/api/v1/properties/:id', () => problem(404)),
      mock.get('*/api/v1/properties/:propertyId/units', () => problem(404)),
    );
    renderRoute(path);

    expect(await screen.findByRole('heading', { name: 'Page not found' })).toBeInTheDocument();
  });
});
