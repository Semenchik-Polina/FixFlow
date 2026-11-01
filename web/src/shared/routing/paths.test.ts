import { describe, expect, it } from 'vitest';
import { firstSegment, isPublicPage, orgSlugFromPath } from './paths';

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

  it.each(['/', '', '/login', '/login/', '/LOGIN', '/organizations', '/organizations/new', '/reset-password'])(
    'finds no slug on the global page %j',
    (pathname) => {
      expect(orgSlugFromPath(pathname)).toBeNull();
    },
  );
});

describe('isPublicPage', () => {
  it.each(['/login', '/login/', '/LOGIN', '/Login//', '/signup', '/Signup', '/forgot-password', '/reset-password/'])(
    'treats %j as a page for signed-out visitors',
    (pathname) => {
      expect(isPublicPage(pathname)).toBe(true);
    },
  );

  it.each(['/', '/acme', '/acme/login', '/login/extra', '/organizations', '/organizations/new'])(
    'treats %j as a page that needs a session',
    (pathname) => {
      expect(isPublicPage(pathname)).toBe(false);
    },
  );
});
