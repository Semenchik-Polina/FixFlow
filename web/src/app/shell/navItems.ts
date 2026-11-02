import HomeOutlined from '@mui/icons-material/HomeOutlined';
import type { Role } from '../../api/generated/model';
import { hasAnyRole } from '../../shared/session/roles';
import { strings } from '../../shared/strings';

export interface NavItem {
  /** Relative to the organization (`/:orgSlug`). */
  to: string;
  label: string;
  Icon: typeof HomeOutlined;
  /** Who sees the item; everyone when absent. UX only: the page's route has the same guard, and the API decides. */
  roles?: readonly Role[];
}

// Later slices add their pages here, with the roles of the route's RequireRoles
export const navItems: readonly NavItem[] = [{ to: '.', label: strings.shell.home, Icon: HomeOutlined }];

/** The items a user with `roles` in the current organization sees. */
export function visibleNavItems(roles: readonly Role[], items: readonly NavItem[] = navItems): NavItem[] {
  return items.filter((item) => item.roles === undefined || hasAnyRole(roles, item.roles));
}
