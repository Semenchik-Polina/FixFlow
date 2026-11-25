import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http as mock } from 'msw';
import { describe, expect, it, vi } from 'vitest';
import {
  getCreatePropertyMockHandler,
  getUpdatePropertyMockHandler,
} from '../../../api/generated/fixflow-api/fixflow-api.msw';
import type { PropertyRequest, PropertyResponse } from '../../../api/generated/model';
import { aProperty, givenPropertyTimeZones, problem, validationProblem } from '../../../test/fixtures';
import { renderWithProviders } from '../../../test/renderWithProviders';
import { server } from '../../../test/server';
import { PropertyFormDialog } from './PropertyFormDialog';

type User = ReturnType<typeof userEvent.setup>;

async function renderDialog(property?: PropertyResponse) {
  givenPropertyTimeZones();
  const onSaved = vi.fn();
  renderWithProviders(<PropertyFormDialog property={property} onClose={vi.fn()} onSaved={onSaved} />);
  // The fields show once the countries have loaded
  await screen.findByLabelText('Name');
  return { onSaved };
}

async function chooseCountry(user: User, typed: string, option: string) {
  const country = screen.getByRole('combobox', { name: 'Country' });
  await user.clear(country);
  await user.type(country, typed);
  await user.click(await screen.findByRole('option', { name: option }));
}

async function fillRequiredFields(user: User) {
  await user.type(screen.getByLabelText('Name'), '  Elm Court ');
  await user.type(screen.getByLabelText('Street address'), '1 Test Street');
  await user.type(screen.getByLabelText('City'), 'Testville');
  await chooseCountry(user, 'Pol', 'Poland (PL)');
}

/** Answers a create with `respond`, keeping the body sent. */
function givenCreate(respond: () => Response) {
  const sent: PropertyRequest[] = [];
  server.use(
    mock.post('*/api/v1/properties', async ({ request }) => {
      sent.push((await request.json()) as PropertyRequest);
      return respond();
    }),
  );
  return sent;
}

describe('PropertyFormDialog', () => {
  it('asks for every required field before calling the API', async () => {
    const user = userEvent.setup();
    const sent = givenCreate(() => problem(500));
    await renderDialog();

    await user.click(screen.getByRole('button', { name: 'Create property' }));

    expect(await screen.findByText("Enter the property's name.")).toBeInTheDocument();
    expect(screen.getByText('Enter the street address.')).toBeInTheDocument();
    expect(screen.getByText('Enter the city.')).toBeInTheDocument();
    expect(screen.getByText('Choose the country.')).toBeInTheDocument();
    expect(screen.getByText('Choose the time zone.')).toBeInTheDocument();
    expect(sent).toEqual([]);
  });

  it('checks the length of the name', async () => {
    const user = userEvent.setup();
    await renderDialog();

    await user.click(screen.getByLabelText('Name'));
    await user.paste('n'.repeat(101));
    await user.click(screen.getByRole('button', { name: 'Create property' }));

    expect(await screen.findByText('Use at most 100 characters.')).toBeInTheDocument();
  });

  it("offers the chosen country's zones only, and fills in a country's only zone", async () => {
    const user = userEvent.setup();
    await renderDialog();
    const timeZone = screen.getByRole('combobox', { name: 'Time zone' });
    expect(timeZone).toBeDisabled();
    expect(screen.getByText('Choose the country first.')).toBeInTheDocument();

    await chooseCountry(user, 'Pol', 'Poland (PL)');
    expect(timeZone).toHaveValue('Europe/Warsaw');

    await chooseCountry(user, 'United', 'United States (US)');
    expect(timeZone).toHaveValue('');
    await user.click(timeZone);
    const zones = within(await screen.findByRole('listbox')).getAllByRole('option');
    expect(zones.map((zone) => zone.textContent)).toEqual([
      'America/Chicago',
      'America/Denver',
      'America/Los_Angeles',
      'America/New_York',
    ]);
  });

  it('creates the property, with blank optional parts as null', async () => {
    const user = userEvent.setup();
    const created = aProperty({ id: '0199b1c2-0000-7000-8000-000000000311', name: 'Elm Court' });
    const sent: PropertyRequest[] = [];
    server.use(
      getCreatePropertyMockHandler(async ({ request }) => {
        sent.push(await request.json());
        return created;
      }),
    );
    const { onSaved } = await renderDialog();

    await fillRequiredFields(user);
    await user.click(screen.getByRole('button', { name: 'Create property' }));

    await vi.waitFor(() => expect(onSaved).toHaveBeenCalledWith(created.id));
    expect(sent).toEqual([
      {
        name: 'Elm Court',
        address: { line1: '1 Test Street', line2: null, city: 'Testville', postalCode: null, countryCode: 'PL' },
        timeZone: 'Europe/Warsaw',
      },
    ]);
  });

  it("shows the API's field errors on their fields, focusing the first", async () => {
    const user = userEvent.setup();
    givenCreate(() =>
      validationProblem({
        'address.city': ["'City' must not contain control characters."],
        timeZone: ["Choose a time zone in the property's country."],
      }),
    );
    await renderDialog();

    await fillRequiredFields(user);
    await user.click(screen.getByRole('button', { name: 'Create property' }));

    expect(await screen.findByText("'City' must not contain control characters.")).toBeInTheDocument();
    expect(screen.getByText("Choose a time zone in the property's country.")).toBeInTheDocument();
    expect(screen.getByLabelText('City')).toHaveFocus();
  });

  it.each([
    [
      'an error on a field the form lacks',
      () => validationProblem({ address: ["'Address' must not be empty."] }),
      "'Address' must not be empty.",
    ],
    ['a server error', () => problem(500), 'Something went wrong on our side. Try again in a moment.'],
  ])('shows %s above the fields', async (_, respond, message) => {
    const user = userEvent.setup();
    givenCreate(respond);
    await renderDialog();

    await fillRequiredFields(user);
    await user.click(screen.getByRole('button', { name: 'Create property' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(message);
  });

  it('edits a property with its values filled in, keeping a stored zone the list lacks', async () => {
    const user = userEvent.setup();
    // Europe/Kiev: an alias the API accepts, which its list of Ukraine's zones doesn't name
    const property = aProperty({
      address: { line1: '2 Test Road', line2: 'Floor 3', city: 'Testograd', postalCode: null, countryCode: 'UA' },
      timeZone: 'Europe/Kiev',
    });
    const sent: { id: string; body: PropertyRequest }[] = [];
    server.use(
      getUpdatePropertyMockHandler(async ({ request, params }) => {
        sent.push({ id: params.id, body: await request.json() });
      }),
    );
    const { onSaved } = await renderDialog(property);

    expect(screen.getByRole('heading', { name: 'Edit property' })).toBeInTheDocument();
    expect(screen.getByLabelText('Apartment, floor or building (optional)')).toHaveValue('Floor 3');
    expect(screen.getByRole('combobox', { name: 'Country' })).toHaveValue('Ukraine (UA)');
    expect(screen.getByRole('combobox', { name: 'Time zone' })).toHaveValue('Europe/Kiev');
    await user.clear(screen.getByLabelText('Name'));
    await user.type(screen.getByLabelText('Name'), 'Riverside Lofts');
    await user.click(screen.getByRole('button', { name: 'Save changes' }));

    await vi.waitFor(() => expect(onSaved).toHaveBeenCalledWith(property.id));
    expect(sent).toEqual([
      {
        id: property.id,
        body: {
          name: 'Riverside Lofts',
          address: { line1: '2 Test Road', line2: 'Floor 3', city: 'Testograd', postalCode: null, countryCode: 'UA' },
          timeZone: 'Europe/Kiev',
        },
      },
    ]);
  });
});
