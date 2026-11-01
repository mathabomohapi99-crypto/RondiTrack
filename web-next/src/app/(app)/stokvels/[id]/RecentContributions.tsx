import { Suspense } from 'react';
import { RowsSkeleton } from '@/components/Skeletons';
import { apiJson } from '@/lib/api';
import { formatCents, formatDateTime } from '@/lib/format';
import type { ApiContribution, ApiPage } from '@/lib/feedMappers';

export type RecentPage = ApiPage<ApiContribution>;

// One small request: the 5 newest contributions of this stokvel (not every contribution ever made).
export function fetchRecent(stokvelId: string): Promise<RecentPage> {
  return apiJson<RecentPage>(`/stokvels/${stokvelId}/contributions?pageSize=5&sort=-date`);
}

export function RecentContributions({ recent }: { recent: Promise<RecentPage> }) {
  return (
    <section>
      <h2 className="mb-2 font-semibold">Recent contributions</h2>
      {/* skeletons first, the rows stream in */}
      <Suspense fallback={<RowsSkeleton rows={3} />}>
        <RecentRows recent={recent} />
      </Suspense>
    </section>
  );
}

// The request was started earlier; it is awaited only HERE
async function RecentRows({ recent }: { recent: Promise<RecentPage> }) {
  const page = await recent;
  if (page.items.length === 0) return <p className="text-sm">No contributions recorded yet.</p>;
  return (
    <ul className="space-y-2">
      {page.items.map((c) => (
        <li key={c.id} className="flex items-center justify-between rounded border p-3">
          <div>
            <p className="font-medium">{c.memberName}</p>
            <p className="text-xs text-gray-500">{formatDateTime(c.recordedAtUtc)}</p>
          </div>
          <span>{formatCents(c.amountCents)}</span>
        </li>
      ))}
    </ul>
  );
}