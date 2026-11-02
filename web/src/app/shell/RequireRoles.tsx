import { Outlet } from 'react-router';
import type { Role } from '../../api/generated/model';
import { NotFoundPage } from '../../shared/components/NotFoundPage';
import { hasAnyRole } from '../../shared/session/roles';
import { useCurrentOrganization } from '../../shared/session/useCurrentOrganization';

interface RequireRolesProps {
  /** The roles that may open the pages below, as the API's role policy for them allows. */
  roles: readonly Role[];
}

/**
 * A layout route that shows its pages only to users with one of `roles` in the current organization, and "Page not
 * found" to others, so a link from another role doesn't open a page full of errors. UX only: the API enforces it.
 */
export function RequireRoles({ roles }: RequireRolesProps) {
  return hasAnyRole(useCurrentOrganization().roles, roles) ? <Outlet /> : <NotFoundPage />;
}
