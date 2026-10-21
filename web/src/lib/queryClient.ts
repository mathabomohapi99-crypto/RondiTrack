import { QueryClient } from '@tanstack/react-query';
import { ApiError } from '../api/apiError';

// Makes a fresh client (tests use this so each test starts clean)
export function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        refetchOnWindowFocus: false,
        // 404, 403, 429 will not fix themselves, so never retry any 4xx
        retry: (failureCount, error) => {
          if (error instanceof ApiError && error.status >= 400 && error.status < 500) return false;
          return failureCount < 3;
        },
      },
    },
  });
}

// The one client the real app uses
export const queryClient = createQueryClient();