import Link from 'next/link';
import { Suspense } from 'react';
import { EmptyState } from '@/components/EmptyState';
import { RowsSkeleton } from '@/components/Skeletons';
import { apiJson } from '@/lib/api';
import { stokvelsWhereTreasurer } from '@/lib/stokvelsWhereTreasurer';
import type { MyStokvel } from '@/lib/types';

export default function TreasurerDashboardPage() {
  return (
    <div>
      <h1 className="mb-4 text-2xl font-bold">Treasurer dashboard</h1>
      <Suspense fallback={<RowsSkeleton rows={2} />}>
        <TreasurerDashboard />
      </Suspense>
    </div>
  );
}

// A FILTERED VIEW, not a door with a role check. Everyone may open it.
// Most people will see the empty state, and that is correct.
async function TreasurerDashboard() {
  const list = await apiJson<MyStokvel[]>('/me/stokvels');
  const mine = stokvelsWhereTreasurer(list); // the role comes from the API's answer for each stokvel
  if (mine.length === 0) {
    return (
      <EmptyState
        message="You don't run any stokvels yet."
        linkHref="/dashboard/member"
        linkText="Go to your member dashboard"
      />
    );
  }
  return (
    <ul className="space-y-2">
      {mine.map((s) => (
        <li key={s.id}>
          <Link href={`/stokvels/${s.id}`} className="underline">
            {s.name}
          </Link>
        </li>
      ))}
    </ul>
  );
}