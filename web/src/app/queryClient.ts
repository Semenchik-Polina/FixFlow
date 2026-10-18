import { QueryClient } from '@tanstack/react-query';
import { ApiError } from '../api/ApiError';

const MAX_RETRIES = 2;

export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        // No realtime in the MVP: data stays fresh by refetching when the tab regains focus (plan A7)
        refetchOnWindowFocus: true,
        // A 4xx won't change on retry; a 5xx or a network failure might
        retry: (failureCount, error) =>
          !(error instanceof ApiError && error.status < 500) && failureCount < MAX_RETRIES,
      },
    },
  });
}
