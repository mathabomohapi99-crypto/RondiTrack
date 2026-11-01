'use client'; // useInfiniteQuery, IntersectionObserver and the Load more button need the browser.

import { useInfiniteQuery } from '@tanstack/react-query';
import { useEffect, useRef } from 'react';
import type { FeedRow } from '@/lib/feedMappers';
import { formatCents, formatDateTime } from '@/lib/format';

type Page = { rows: FeedRow[]; nextPageToken: string };

type Props = {
  keyPrefix: 'contributions' | 'activity'; // first part of the query key
  endpoint: string; // OUR site's Route Handler, e.g. /bff/activity. Never the API.
  baseQuery: string; // the filters as a query string (no page token)
  initialRows: FeedRow[]; // page one, rendered by the server
  initialToken: string; // the token page one ended with ('' = that was everything)
  noun: string;
};

async function fetchPage(url: string): Promise<Page> {
  const res = await fetch(url, { headers: { Accept: 'application/json' } }); // same origin: the Next.js server
  if (res.status === 401) throw new Error('SESSION_ENDED');
  if (!res.ok) throw new Error('LOAD_FAILED');
  return (await res.json()) as Page;
}

export function FeedList({ keyPrefix, endpoint, baseQuery, initialRows, initialToken, noun }: Props) {
  const query = useInfiniteQuery({
    // Everything the list is filtered by is in the key (baseQuery), plus the token page one ended with.
    // A filter change = a different key = a fresh list from page one. An old page can never show under a new filter.
    queryKey: [keyPrefix, endpoint, baseQuery, initialToken],
    queryFn: ({ pageParam }) =>
      fetchPage(`${endpoint}?${baseQuery}${baseQuery ? '&' : ''}pageToken=${encodeURIComponent(pageParam)}`),
    initialPageParam: initialToken,
    getNextPageParam: (last) => (last.nextPageToken ? last.nextPageToken : undefined), // empty token = the end
    enabled: false, // page one came from the server; more pages load only when asked (scroll or button)
  });

  const { fetchNextPage, isFetching, isFetchingNextPage, isError, error } = query;

  // Before the first extra page arrives there is no data yet: then the server's token decides
  const hasMore = query.data ? query.hasNextPage : initialToken !== '';

  // Server rows first, then the loaded pages (a row id is never shown twice)
  const seen = new Set<string>();
  const rows: FeedRow[] = [];
  for (const row of [...initialRows, ...(query.data?.pages.flatMap((p) => p.rows) ?? [])]) {
    if (!seen.has(row.id)) {
      seen.add(row.id);
      rows.push(row);
    }
  }

  // Infinite scroll: when the end of the list scrolls into view, load the next page
  const sentinel = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const el = sentinel.current;
    if (!el || !hasMore) return;
    const observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((e) => e.isIntersecting) && !isFetching) void fetchNextPage();
      },
      { rootMargin: '200px' },
    );
    observer.observe(el);
    return () => observer.disconnect();
  }, [hasMore, isFetching, fetchNextPage]);

  const sessionEnded = isError && error instanceof Error && error.message === 'SESSION_ENDED';

  // Says "all" ONLY when the API's token really is empty
  const statusText = isFetchingNextPage
    ? `Loading more… ${rows.length} ${noun} showing so far.`
    : hasMore
      ? `Showing ${rows.length} ${noun}. There are more.`
      : `Showing all ${rows.length} ${noun}.`;

  return (
    <div className="space-y-3">
      <ul className="space-y-2">
        {rows.map((row) => (
          <li key={row.id} className="flex items-center justify-between rounded border p-3">
            <div>
              <p className="font-medium">{row.primary}</p>
              <p className="text-xs text-gray-500">{row.secondary}</p>
              <p className="text-xs text-gray-500">{formatDateTime(row.whenUtc)}</p>
            </div>
            {row.amountCents !== null && <span>{formatCents(row.amountCents)}</span>}
          </li>
        ))}
      </ul>

      <div ref={sentinel} aria-hidden="true" />

      <p role="status" aria-live="polite" className="text-sm text-gray-600">{statusText}</p>

      {isError && (
        <p role="alert" className="text-sm text-red-600">
          {sessionEnded ? 'Your session ended. Please sign in again.' : 'We could not load more. Please try again.'}
        </p>
      )}

      {/* A real button, so a keyboard can load more too. It stays visible while a page loads. */}
      {hasMore && (
        <button type="button" disabled={isFetching} onClick={() => void fetchNextPage()}
          className="rounded border px-4 py-2 disabled:opacity-50">
          {isFetchingNextPage ? 'Loading…' : isError ? 'Try again' : 'Load more'}
        </button>
      )}
    </div>
  );
}