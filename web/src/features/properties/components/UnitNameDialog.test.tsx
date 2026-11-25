import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http as mock } from 'msw';
import { describe, expect, it, vi } from 'vitest';
import { getRenameUnitMockHandler } from '../../../api/generated/fixflow-api/fixflow-api.msw';
import type { UnitRequest } from '../../../api/generated/model';
import { aProperty, aUnit, validationProblem } from '../../../test/fixtures';
import { renderWithProviders } from '../../../test/renderWithProviders';
import { server } from '../../../test/server';
import { UnitNameDialog } from './UnitNameDialog';

const property = aProperty();

describe('UnitNameDialog', () => {
  it('asks for a name of at most 50 characters before calling the API', async () => {
    const user = userEvent.setup();
    const create = vi.fn();
    server.use(mock.post('*/api/v1/properties/:propertyId/units', create));
    renderWithProviders(<UnitNameDialog propertyId={property.id} onClose={vi.fn()} />);

    await user.click(screen.getByRole('button', { name: 'Add unit' }));
    expect(await screen.findByText("Enter the unit's name.")).toBeInTheDocument();

    await user.type(screen.getByLabelText('Unit name'), 'u'.repeat(51));
    expect(await screen.findByText('Use at most 50 characters.')).toBeInTheDocument();
    expect(create).not.toHaveBeenCalled();
  });

  it("shows the API's error when another unit has the name", async () => {
    const user = userEvent.setup();
    server.use(
      mock.post('*/api/v1/properties/:propertyId/units', () =>
        validationProblem({ name: ['This property already has a unit named 4B.'] }, 422),
      ),
    );
    const onClose = vi.fn();
    renderWithProviders(<UnitNameDialog propertyId={property.id} onClose={onClose} />);

    await user.type(screen.getByLabelText('Unit name'), '4B');
    await user.click(screen.getByRole('button', { name: 'Add unit' }));

    expect(await screen.findByText('This property already has a unit named 4B.')).toBeInTheDocument();
    expect(screen.getByLabelText('Unit name')).toHaveFocus();
    expect(onClose).not.toHaveBeenCalled();
  });

  it('renames a unit, starting from its name', async () => {
    const user = userEvent.setup();
    const unit = aUnit(property.id, '4B');
    const sent: { unitId: string; body: UnitRequest }[] = [];
    server.use(
      getRenameUnitMockHandler(async ({ request, params }) => {
        sent.push({ unitId: params.unitId, body: await request.json() });
      }),
    );
    const onClose = vi.fn();
    renderWithProviders(<UnitNameDialog propertyId={property.id} unit={unit} onClose={onClose} />);

    expect(screen.getByRole('heading', { name: 'Rename unit' })).toBeInTheDocument();
    await user.clear(screen.getByLabelText('Unit name'));
    await user.type(screen.getByLabelText('Unit name'), ' 4C ');
    await user.click(screen.getByRole('button', { name: 'Save name' }));

    await vi.waitFor(() => expect(onClose).toHaveBeenCalled());
    expect(sent).toEqual([{ unitId: unit.id, body: { name: '4C' } }]);
  });
});
