import { Suspense } from 'react';
import { EmptyState } from '@/components/EmptyState';
import { FeedList } from '@/components/FeedList';
import { RowsSkeleton } from '@/components/Skeletons';
import { apiJson } from '@/lib/api';
import { toActivityRow, type ApiActivity, type ApiPage } from '@/lib/feedMappers';

export default function ActivityPage() {
  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-bold">Activity</h1>
      <Suspense fallback={<RowsSkeleton rows={4} />}>
        <Activity />
      </Suspense>
    </div>
  );
}

// Page one from the server. The rest load from /bff/activity as the member scrolls.
async function Activity() {
  const page = await apiJson<ApiPage<ApiActivity>>('/me/activity?pageSize=20');
  const rows = page.items.map(toActivityRow);
  if (rows.length === 0) return <EmptyState message="No activity in your stokvels yet." />;
  return (
    <FeedList keyPrefix="activity" endpoint="/bff/activity" baseQuery="" initialRows={rows}
      initialToken={page.nextPageToken} noun="items" />
  );
}