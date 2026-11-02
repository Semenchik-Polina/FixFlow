import type { QueryClient } from '@tanstack/react-query';
import { createBrowserRouter, type RouteObject } from 'react-router';
import { NotFoundPage } from '../shared/components/NotFoundPage';
import { PageLoader } from '../shared/components/PageLoader';
import { RouteErrorPage } from '../shared/components/RouteErrorPage';
import { globalPaths } from '../shared/routing/paths';
import { sessionQueryOptions } from '../shared/session/useCurrentUser';
import { HomeRedirect } from './HomeRedirect';

/**
 * Global pages first; everything else lives under an organization slug (ARCHITECTURE §8). Pages load lazily.
 * A function, not a shared constant: a router updates the route objects it loads lazily, so each router (the app's,
 * and one per test) needs its own.
 * @param queryClient The global query client, which holds the session (`/me`).
 */
export function createRoutes(queryClient: QueryClient): RouteObject[] {
  // For the routes that read the session: it starts loading with the page's code instead of after it, and the page's
  // own query picks it up. Never awaited, so a failure is the page's to show.
  const loadSession = () => {
    void queryClient.prefetchQuery(sessionQueryOptions());
    return null;
  };

  return [
    {
      HydrateFallback: PageLoader,
      ErrorBoundary: RouteErrorPage,
      children: [
        { index: true, loader: loadSession, Component: HomeRedirect },
        {
          path: globalPaths.login,
          lazy: { Component: async () => (await import('../features/auth/pages/LoginPage')).LoginPage },
        },
        {
          path: globalPaths.signup,
          lazy: { Component: async () => (await import('../features/auth/pages/SignupPage')).SignupPage },
        },
        {
          path: globalPaths.forgotPassword,
          lazy: {
            Component: async () => (await import('../features/auth/pages/ForgotPasswordPage')).ForgotPasswordPage,
          },
        },
        {
          path: globalPaths.resetPassword,
          lazy: {
            Component: async () => (await import('../features/auth/pages/ResetPasswordPage')).ResetPasswordPage,
          },
        },
        {
          path: globalPaths.organizations,
          loader: loadSession,
          lazy: {
            Component: async () =>
              (await import('../features/organizations/pages/OrganizationPickerPage')).OrganizationPickerPage,
          },
        },
        {
          path: globalPaths.newOrganization,
          lazy: {
            Component: async () =>
              (await import('../features/organizations/pages/CreateOrganizationPage')).CreateOrganizationPage,
          },
        },
        {
          path: ':orgSlug',
          loader: loadSession,
          lazy: { Component: async () => (await import('./shell/OrganizationLayout')).OrganizationLayout },
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

export function createAppRouter(queryClient: QueryClient) {
  return createBrowserRouter(createRoutes(queryClient));
}
