import { randomBytes } from 'node:crypto';

// Unique fake values (D23: *.example.test, generated names), like the integration tests' TestData. Every test signs up
// its own organization from these, which keeps parallel tests apart without resetting anything.

export interface Account {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  organizationName: string;
  slug: string;
}

export const email = () => `user-${token()}@example.test`;

export const organizationSlug = () => `org-${token()}`;

export const name = (prefix: string) => `${prefix} ${token()}`;

// Made at runtime, so no password literal sits in the repo
export const password = () => `phrase ${token()} ${token()}`;

export const aNewAccount = (): Account => ({
  firstName: name('Test'),
  lastName: name('User'),
  email: email(),
  password: password(),
  organizationName: name('Organization'),
  slug: organizationSlug(),
});

const token = () => randomBytes(6).toString('hex');
