import type { Role } from '../../api/generated/model';

/** Whether the user holds any of the `allowed` roles. Navigation and route guards use it, for UX only: the API decides. */
export function hasAnyRole(roles: readonly Role[], allowed: readonly Role[]): boolean {
  return roles.some((role) => allowed.includes(role));
}
