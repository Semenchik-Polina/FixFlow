import { signIn } from '../support/auth';
import { expect, test } from '../support/fixtures';
import { resetLinkIn } from '../support/mailpit';
import { password } from '../support/testData';

test('resets the password through the emailed link', async ({ page, signedUpAccount: account, mailpit }) => {
  await page.goto('/forgot-password');
  await page.getByLabel('Email').fill(account.email);
  await page.getByRole('button', { name: 'Send reset link' }).click();
  await expect(page.getByRole('heading', { level: 1, name: 'Check your email' })).toBeVisible();

  const message = await mailpit.messageTo(account.email);
  expect(message.Subject).toBe('Reset your FixFlow password');
  const link = resetLinkIn(message.Text);
  // Links come from Spa:PublicUrl, which the `full` profile sets to the address the tests use
  expect(link).toMatch(/^https:\/\/localhost:8443\/reset-password#token=/);

  const newPassword = password();
  await page.goto(link);
  await page.getByLabel('New password', { exact: true }).fill(newPassword);
  await page.getByLabel('Confirm new password', { exact: true }).fill(newPassword);
  await page.getByRole('button', { name: 'Set new password' }).click();

  await expect(page).toHaveURL('/login');
  await expect(page.getByRole('alert')).toHaveText('Your password is changed. Sign in with the new one.');

  await signIn(page, { email: account.email, password: newPassword });
  await expect(page).toHaveURL(`/${account.slug}`);
});
