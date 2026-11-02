import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useState, type ReactNode } from 'react';
import { useGlobalQueryClient } from '../../shared/session/globalQueryClient';

/**
 * Gives one organization's pages a query cache of their own (plan P35), so data of one organization can never show
 * in another. The layout renders it keyed by the slug: a switch mounts a new scope with an empty client, and the old
 * client, once unmounted, is read by nothing. Its requests still in flight are aborted as their pages unmount (the
 * generated queries pass on their abort signal). `http.ts` sends the slug of the URL at the time of the request, so a
 * refetch the old organization's client starts during the switch may carry the new slug; its answer lands in the old
 * client and is dropped with it.
 * Data of no organization (the session, `/me`) stays in the global client.
 */
export function OrganizationQueryScope({ children }: { children: ReactNode }) {
  const globalClient = useGlobalQueryClient();
  // Same defaults as the global client (retries, refetch on focus), which tests make non-retrying
  const [client] = useState(() => new QueryClient({ defaultOptions: globalClient.getDefaultOptions() }));

  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}
