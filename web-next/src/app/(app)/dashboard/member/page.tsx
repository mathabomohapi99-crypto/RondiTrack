import { Suspense } from 'react';
import { ContributionCard } from '@/components/ContributionCard';
import { EmptyState } from '@/components/EmptyState';
import { CardGridSkeleton } from '@/components/Skeletons';
import { apiJson } from '@/lib/api';
import type { MyStokvel } from '@/lib/types';

// The page returns at once; the API call happens inside <Suspense>.
export default function MemberDashboardPage() {
  return (
    <div>
      <h1 className="mb-4 text-2xl font-bold">Member dashboard</h1>
      <Suspense fallback={<CardGridSkeleton />}>
        <MemberDashboard />
      </Suspense>
    </div>
  );
}

// Every stokvel the person is in, whatever their role: paid, owed, next payout.
async function MemberDashboard() {
  const list = await apiJson<MyStokvel[]>('/me/stokvels');
  if (list.length === 0) return <EmptyState message="You are not in any stokvels yet." />;
  return (
    <div className="grid gap-4 md:grid-cols-2">
      {list.map((s) => (
        <ContributionCard key={s.id} stokvel={s} />
      ))}
    </div>
  );
}