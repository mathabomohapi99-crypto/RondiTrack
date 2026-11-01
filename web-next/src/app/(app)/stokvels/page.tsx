import { Suspense } from 'react';
import { EmptyState } from '@/components/EmptyState';
import { CardGridSkeleton } from '@/components/Skeletons';
import { StokvelCard } from '@/components/StokvelCard';
import { apiJson } from '@/lib/api';
import type { MyStokvel } from '@/lib/types';

export default function StokvelsPage() {
  return (
    <div>
      <h1 className="mb-4 text-2xl font-bold">My stokvels</h1>
      <Suspense fallback={<CardGridSkeleton />}>
        <MyStokvels />
      </Suspense>
    </div>
  );
}

// Every stokvel the person belongs to, with their role in each.
async function MyStokvels() {
  const list = await apiJson<MyStokvel[]>('/me/stokvels');
  if (list.length === 0) return <EmptyState message="You are not in any stokvels yet." />;
  return (
    <div className="grid gap-4 md:grid-cols-2">
      {list.map((s) => (
        <StokvelCard key={s.id} stokvel={s} />
      ))}
    </div>
  );
}