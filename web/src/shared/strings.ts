// UI text lives outside components (D27): English only for now, so localization can be added later.
// Strings used by one feature only go in that feature's strings.ts.
export const strings = {
  app: {
    name: 'FixFlow',
  },
  common: {
    loading: 'Loading',
    reload: 'Reload',
  },
  errors: {
    generic: 'Something went wrong',
    genericDetail: 'Reload the page to try again.',
    notFound: 'Page not found',
    notFoundDetail: "The page you're looking for doesn't exist or has moved.",
  },
  shell: {
    openNavigation: 'Open navigation',
    navigation: 'Main navigation',
    home: 'Home',
  },
} as const;
