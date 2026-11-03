import HomeOutlined from '@mui/icons-material/HomeOutlined';
import { describe, expect, it } from 'vitest';
import type { Role } from '../../api/generated/model';
import { navItems, visibleNavItems, type NavItem } from './navItems';

const items: NavItem[] = [
  { to: '.', label: 'Home', Icon: HomeOutlined },
  { to: 'properties', label: 'Properties', Icon: HomeOutlined, roles: ['Manager', 'Admin'] },
  { to: 'jobs', label: 'Jobs', Icon: HomeOutlined, roles: ['Technician'] },
];

describe('visibleNavItems', () => {
  it.each<[Role[], string[]]>([
    [['Tenant'], ['Home']],
    [['Technician'], ['Home', 'Jobs']],
    [['Admin'], ['Home', 'Properties']],
    [
      ['Manager', 'Technician'],
      ['Home', 'Properties', 'Jobs'],
    ],
  ])('shows a user with roles %j the items %j', (roles, labels) => {
    expect(visibleNavItems(roles, items).map((item) => item.label)).toEqual(labels);
  });

  it('shows Home to every role', () => {
    expect(visibleNavItems(['Tenant'], navItems).map((item) => item.label)).toContain('Home');
  });
});
