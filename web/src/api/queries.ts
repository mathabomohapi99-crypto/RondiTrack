import { useInfiniteQuery, useQuery } from '@tanstack/react-query';
import { api, unwrap } from './client';
import { queryKeys } from './queryKeys';

// Components only use these hooks, never api.GET or fetch directly.
// Removed useMe: my API has no /api/v1/me endpoint (noted in README Known gaps).

export function useHome() {
  return useQuery({
    queryKey: queryKeys.home,
    queryFn: async () => unwrap(await api.GET('/api/v1/me/home')),
  });
}

export function useStokvels() {
  return useQuery({
    queryKey: queryKeys.stokvels,
    queryFn: async () => unwrap(await api.GET('/api/v1/stokvels')),
  });
}

export function useStokvel(id: string) {
  return useQuery({
    queryKey: queryKeys.stokvel(id),
    // Changed: the path parameter is called stokvelId in my API
    queryFn: async () =>
      unwrap(await api.GET('/api/v1/stokvels/{stokvelId}', { params: { path: { stokvelId: id } } })),
  });
}

export function useCycles(id: string) {
  return useQuery({
    queryKey: queryKeys.cycles(id),
    queryFn: async () =>
      unwrap(
        await api.GET('/api/v1/stokvels/{stokvelId}/cycles', { params: { path: { stokvelId: id } } }),
      ),
  });
}

// Contributions are paged and belong to one cycle of one stokvel
export function useContributions(stokvelId: string, cycleId: string | undefined) {
  return useInfiniteQuery({
    queryKey: queryKeys.contributions(stokvelId, cycleId ?? ''),
    enabled: cycleId !== undefined, // wait until we know which cycle to load
    // NO placeholderData: keepPreviousData here. It would show the OLD stokvel's rows
    // under the NEW stokvel's key (the Part 4 trap).
    initialPageParam: undefined as string | undefined,
    queryFn: async ({ pageParam }) =>
      unwrap(
        await api.GET('/api/v1/stokvels/{stokvelId}/cycles/{cycleId}/contributions', {
          params: {
            path: { stokvelId, cycleId: cycleId ?? '' },
            query: { PageToken: pageParam }, // capital P, exactly as in my schema
          },
        }),
      ),
    // "" and null both mean "no next page" (check 4), so || undefined handles both
    getNextPageParam: (lastPage) => lastPage.nextPageToken || undefined,
  });
}