import { globalPaths } from '../shared/routing/paths';

/** A full page load, not a client-side navigation: it also drops every cached query of the ended session. */
export function redirectToLogin(): void {
  window.location.assign(globalPaths.login);
}
