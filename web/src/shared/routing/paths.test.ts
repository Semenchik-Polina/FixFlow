import { describe, expect, it } from 'vitest';
import { firstSegment, orgSlugFromPath } from './paths';

describe('firstSegment', () => {
  it.each([
    ['/organizations/new', 'organizations'],
    ['/login', 'login'],
    ['/acme/', 'acme'],
    ['/', ''],
    ['', ''],
  ])('reads %j as %j', (pathname, segment) => {
    expect(firstSegment(pathname)).toBe(segment);
  });
});

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
