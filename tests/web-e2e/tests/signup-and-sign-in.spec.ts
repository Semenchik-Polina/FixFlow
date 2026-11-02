import { signIn, signOut } from '../support/auth';
import { expect, test } from '../support/fixtures';
import { aNewAccount } from '../support/testData';

test('signs up, lands on the new organization, and signs out and back in', async ({ page }) => {
  const account = aNewAccount();

  await page.goto('/signup');
  await page.getByLabel('First name').fill(account.firstName);
  await page.getByLabel('Last name').fill(account.lastName);
  await page.getByLabel('Email').fill(account.email);
  await page.getByLabel('Password', { exact: true }).fill(account.password);
  await page.getByLabel('Organization name').fill(account.organizationName);
  // Suggested from the name; set explicitly, so the test knows the address
  await page.getByLabel('Web address').fill(account.slug);
  await page.getByRole('button', { name: 'Create account' }).click();

  await expect(page).toHaveURL(`/${account.slug}`);
  await expect(page.getByRole('heading', { level: 1, name: 'Home' })).toBeVisible();
  await expect(page.getByRole('button', { name: `${account.organizationName}, switch organization` })).toBeVisible();

  await signOut(page);
  await expect(page).toHaveURL('/login');

  await signIn(page, account);
  await expect(page).toHaveURL(`/${account.slug}`);
  await expect(page.getByRole('heading', { level: 1, name: 'Home' })).toBeVisible();
});
