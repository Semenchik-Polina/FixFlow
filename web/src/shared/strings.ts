import type { Role } from '../api/generated/model';

// UI text lives outside components (D27): English only for now, so localization can be added later.
// Strings used by one feature only go in that feature's strings.ts.
export const strings = {
  app: {
    name: 'FixFlow',
  },
  /** The browser tab's title: the page first, so tabs in different organizations tell apart. */
  pageTitle: (page: string) => `${page} – FixFlow`,
  common: {
    loading: 'Loading',
    reload: 'Reload',
    retry: 'Try again',
  },
  errors: {
    generic: 'Something went wrong',
    genericDetail: 'Reload the page to try again.',
    notFound: 'Page not found',
    notFoundDetail: "The page you're looking for doesn't exist or has moved.",
    serverError: 'Something went wrong on our side. Try again in a moment.',
    unreachable: "FixFlow can't be reached. Check your connection, then try again.",
    tooManyAttempts: 'Too many attempts. Wait a minute, then try again.',
  },
  forms: {
    showPassword: 'Show password',
    hidePassword: 'Hide password',
  },
  // The server has the final say; these mirror its rules (shared/forms/rules.ts)
  validation: {
    emailRequired: 'Enter your email.',
    emailInvalid: 'Enter an email address like name@example.com.',
    passwordRequired: 'Enter your password.',
    passwordTooShort: (min: number) => `Use at least ${min} characters.`,
    firstNameRequired: 'Enter your first name.',
    lastNameRequired: 'Enter your last name.',
    organizationNameRequired: "Enter your organization's name.",
    slugRequired: 'Enter a web address.',
    slugLength: (min: number, max: number) => `Use ${min} to ${max} characters.`,
    slugFormat: 'Use lowercase letters, digits and single hyphens, starting and ending with a letter or digit.',
    tooLong: (max: number) => `Use at most ${max} characters.`,
  },
  organization: {
    name: 'Organization name',
    slug: 'Web address',
    slugHint: (address: string) => `Your organization's address: ${address}. It can't be changed later.`,
    slugExample: 'your-organization',
  },
  session: {
    signedInAs: (email: string) => `Signed in as ${email}.`,
    signOut: 'Sign out',
    signOutFailed: "You're still signed in: FixFlow couldn't sign you out. Check your connection, then try again.",
  },
  roles: {
    Tenant: 'Tenant',
    Technician: 'Technician',
    Manager: 'Manager',
    Admin: 'Admin',
  } satisfies Record<Role, string>,
  shell: {
    openNavigation: 'Open navigation',
    navigation: 'Main navigation',
    home: 'Home',
    // Starts with the visible name, so a voice command that says what's on screen still finds the button
    switchOrganization: (organizationName: string) => `${organizationName}, switch organization`,
    organizations: 'Organizations',
    allOrganizations: 'All organizations',
    createOrganization: 'Create organization',
    accountMenu: 'Account',
  },
} as const;
