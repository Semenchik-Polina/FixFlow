import { createBrowserRouter, Navigate, type RouteObject } from 'react-router';
import { NotFoundPage } from '../shared/components/NotFoundPage';
import { PageLoader } from '../shared/components/PageLoader';
import { RouteErrorPage } from '../shared/components/RouteErrorPage';
import { globalPaths } from '../shared/routing/paths';

/**
 * Global pages first; everything else lives under an organization slug (ARCHITECTURE §8). Pages load lazily.
 * A function, not a shared constant: a router updates the route objects it loads lazily, so each router (the app's,
 * and one per test) needs its own.
 */
export function createRoutes(): RouteObject[] {
  return [
    {
      HydrateFallback: PageLoader,
      ErrorBoundary: RouteErrorPage,
      children: [
        // The organization picker replaces this once there is a session to read (plan T1.7)
        { index: true, element: <Navigate to={globalPaths.login} replace /> },
        {
          path: globalPaths.login,
          lazy: { Component: async () => (await import('../features/auth/pages/LoginPage')).LoginPage },
        },
        {
          path: ':orgSlug',
          lazy: { Component: async () => (await import('./shell/AppShell')).AppShell },
          children: [
            {
              index: true,
              lazy: {
                Component: async () => (await import('../features/dashboard/pages/DashboardPage')).DashboardPage,
              },
            },
            { path: '*', Component: NotFoundPage },
          ],
        },
      ],
    },
  ];
}

export function createAppRouter() {
  return createBrowserRouter(createRoutes());
}
