export const propertyStrings = {
  list: {
    title: 'Properties',
    create: 'New property',
    search: 'Search by name',
    empty: 'No properties yet. Add your first building with New property.',
    noMatch: 'No property has this in its name.',
  },
  columns: {
    name: 'Name',
    address: 'Address',
    timeZone: 'Time zone',
    actions: 'Actions',
  },
  form: {
    createTitle: 'New property',
    editTitle: 'Edit property',
    name: 'Name',
    streetAddress: 'Street address',
    addressLine2: 'Apartment, floor or building (optional)',
    city: 'City',
    postalCode: 'Postal code (optional)',
    country: 'Country',
    timeZone: 'Time zone',
    timeZoneHint: 'Due dates and appointments use this time zone.',
    chooseCountryFirst: 'Choose the country first.',
    loadingCountries: 'Loading countries',
    create: 'Create property',
    save: 'Save changes',
  },
  detail: {
    allProperties: 'All properties',
    edit: 'Edit',
    archive: 'Archive',
    timeZone: (timeZone: string) => `Time zone: ${timeZone}`,
    archiveTitle: (name: string) => `Archive ${name}?`,
    archiveMessage: (unitCount: number) =>
      `${archivedUnits(unitCount)}Archived properties leave every list, and FixFlow can't restore them.`,
    archiveConfirm: 'Archive property',
  },
  units: {
    title: 'Units',
    add: 'Add unit',
    empty: 'No units yet. Add the apartments, offices or other spaces tenants rent.',
    name: 'Unit name',
    addTitle: 'Add unit',
    renameTitle: 'Rename unit',
    save: 'Save name',
    rename: (name: string) => `Rename ${name}`,
    archive: (name: string) => `Archive ${name}`,
    archiveTitle: (name: string) => `Archive unit ${name}?`,
    archiveMessage: "Archived units leave this property's list, and FixFlow can't restore them.",
    archiveConfirm: 'Archive unit',
  },
};

function archivedUnits(count: number): string {
  if (count === 0) {
    return '';
  }

  return count === 1 ? 'Its unit is archived with it. ' : `Its ${count} units are archived with it. `;
}
