import { describe, expect, it } from 'vitest';
import { orgSlugFromPath } from './paths';

describe('orgSlugFromPath', () => {
  it.each([
    ['/acme', 'acme'],
    ['/acme/', 'acme'],
    ['/acme/requests/0199b1c2-0000-7000-8000-000000000001', 'acme'],
    ['/brightway-homes/work-orders', 'brightway-homes'],
  ])('reads the slug of %s', (pathname, slug) => {
    expect(orgSlugFromPath(pathname)).toBe(slug);
  });

  it.each(['/', '', '/login', '/login/', '/LOGIN'])('finds no slug on the global page %j', (pathname) => {
    expect(orgSlugFromPath(pathname)).toBeNull();
  });
});
