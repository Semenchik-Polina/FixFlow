import type { QueryClient } from '@tanstack/react-query';
import { createContext, useContext } from 'react';

/**
 * The query client for data that belongs to no organization: the session (`/me`). Each organization's pages get a
 * client of their own (`OrganizationQueryScope`), where `useQueryClient()` returns that one, so code under `/:orgSlug`
 * reaches this client only through {@link useGlobalQueryClient}.
 */
export const GlobalQueryClientContext = createContext<QueryClient | null>(null);

export function useGlobalQueryClient(): QueryClient {
  const client = useContext(GlobalQueryClientContext);
  if (client === null) {
    throw new Error('GlobalQueryClientContext is not provided.');
  }

  return client;
}
