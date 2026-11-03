import { test as base, expect } from '@playwright/test';
import { Mailpit, mailpitUrl } from './mailpit';
import { aNewAccount, type Account } from './testData';

interface Fixtures {
  /**
   * A new account and organization, signed up through the API, for tests whose subject isn't signup (plan P36).
   * Playwright's `request` has its own cookies, so the page stays signed out.
   */
  signedUpAccount: Account;
  mailpit: Mailpit;
}

export const test = base.extend<Fixtures>({
  signedUpAccount: async ({ request }, use) => {
    const account = aNewAccount();
    const { email, password, firstName, lastName, organizationName, slug } = account;
    const response = await request.post('/api/v1/auth/signup', {
      data: { email, password, firstName, lastName, organizationName, slug },
    });
    expect(response.status(), 'signup through the API').toBe(201);
    await use(account);
  },
  mailpit: async ({ playwright }, use) => {
    const api = await playwright.request.newContext({ baseURL: mailpitUrl });
    await use(new Mailpit(api));
    await api.dispose();
  },
});

export { expect };
