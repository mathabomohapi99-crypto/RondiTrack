import Link from 'next/link';
import { Suspense } from 'react';
import { EmptyState } from '@/components/EmptyState';
import { FeedList } from '@/components/FeedList';
import { RowsSkeleton } from '@/components/Skeletons';
import { apiJson } from '@/lib/api';
import { loadContributionFilters } from '@/lib/contributionSearchParams';
import { contributionsPath } from '@/lib/contributionsPath';
import { toContributionRow, type ApiContribution, type ApiPage } from '@/lib/feedMappers';
import { fetchMembers } from '@/lib/members';
import { ContributionFilters } from './ContributionFilters';

const PAGE_SIZE = 20;

type Props = PageProps<'/stokvels/[id]/contributions'>;

// Returns at once. The URL, the session and the API are read inside <Suspense>.
export default function ContributionHistoryPage({ params, searchParams }: Props) {
  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-bold">Contribution history</h1>
      <Suspense fallback={<RowsSkeleton rows={4} />}>
        <History params={params} searchParams={searchParams} />
      </Suspense>
    </div>
  );
}

async function History({ params, searchParams }: { params: Props['params']; searchParams: Props['searchParams'] }) {
  const { id } = await params;
  // The server renders page one from the URL. Nonsense in the URL falls back to the defaults.
  const filters = await loadContributionFilters(searchParams);
  const { path, rangeIgnored } = contributionsPath(id, filters, PAGE_SIZE);

  const [page, members] = await Promise.all([apiJson<ApiPage<ApiContribution>>(path), fetchMembers(id)]);
  const rows = page.items.map(toContributionRow);

  // Filters only (no page token): the browser adds the token itself when it loads more
  const baseQuery = new URLSearchParams({ stokvelId: id, sort: filters.sort });
  if (filters.member) baseQuery.set('member', filters.member);
  if (filters.from) baseQuery.set('from', filters.from);
  if (filters.to) baseQuery.set('to', filters.to);

  return (
    <>
      <p>
        <Link href={`/stokvels/${id}`} className="underline">Back to the stokvel</Link>
      </p>
      <Suspense fallback={<p className="text-sm">Loading filters…</p>}>
        <ContributionFilters members={members} />
      </Suspense>
      {rangeIgnored && (
        <p className="text-sm text-amber-700">The end date is before the start date, so both dates were ignored.</p>
      )}
      {rows.length === 0 ? (
        // Not an error: it says so and offers a way back to everything
        <EmptyState
          message="No contributions match these filters."
          linkHref={`/stokvels/${id}/contributions`}
          linkText="Show everything"
        />
      ) : (
        <FeedList
          keyPrefix="contributions"
          endpoint="/bff/contributions"
          baseQuery={baseQuery.toString()}
          initialRows={rows}
          initialToken={page.nextPageToken}
          noun="contributions"
        />
      )}
    </>
  );
}