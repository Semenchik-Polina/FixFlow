import { signIn } from '../support/auth';
import { expect, test } from '../support/fixtures';
import { name } from '../support/testData';

test('the admin creates a property and a unit', async ({ page, signedUpAccount }) => {
  const propertyName = name('Property');
  const unitName = name('Unit');

  await signIn(page, signedUpAccount);
  await page.getByRole('link', { name: 'Properties' }).click();
  await expect(page.getByRole('heading', { level: 1, name: 'Properties' })).toBeVisible();

  await page.getByRole('button', { name: 'New property' }).click();
  const propertyDialog = page.getByRole('dialog', { name: 'New property' });
  await propertyDialog.getByLabel('Name', { exact: true }).fill(propertyName);
  await propertyDialog.getByLabel('Street address').fill('1 Test Street');
  await propertyDialog.getByLabel('City').fill('Testville');
  await propertyDialog.getByRole('combobox', { name: 'Country' }).fill('Poland');
  await page.getByRole('option', { name: 'Poland (PL)' }).click();
  // Poland has one time zone, which the form fills in
  await expect(propertyDialog.getByRole('combobox', { name: 'Time zone' })).toHaveValue('Europe/Warsaw');
  await propertyDialog.getByRole('button', { name: 'Create property' }).click();

  await expect(page).toHaveURL(new RegExp(`^.*/${signedUpAccount.slug}/properties/[0-9a-f-]{36}$`));
  await expect(page.getByRole('heading', { level: 1, name: propertyName })).toBeVisible();
  await expect(page.getByText('Time zone: Europe/Warsaw')).toBeVisible();

  await page.getByRole('button', { name: 'Add unit' }).click();
  const unitDialog = page.getByRole('dialog', { name: 'Add unit' });
  await unitDialog.getByLabel('Unit name').fill(unitName);
  await unitDialog.getByRole('button', { name: 'Add unit' }).click();
  await expect(unitDialog).toBeHidden();
  const units = page.getByRole('grid', { name: 'Units' });
  await expect(units.getByText(unitName)).toBeVisible();

  // Saved, not only shown
  await page.reload();
  await expect(page.getByRole('heading', { level: 1, name: propertyName })).toBeVisible();
  await expect(units.getByText(unitName)).toBeVisible();
});
