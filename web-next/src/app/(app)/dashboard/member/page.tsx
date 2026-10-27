import { api, unwrap } from '@/lib/api';
import { ContributionCard } from '@/components/ContributionCard';
import { EmptyState } from '@/components/EmptyState';
import type { MyStokvel } from '@/lib/types';

// Every stokvel the person is in, whatever their role: paid, owed, next payout.
export default async function MemberDashboardPage() {
  const client = await api();
  const list = unwrap<MyStokvel[]>(await client.GET('/api/me/stokvels'));

  return (
    <div>
      <h1 className="mb-4 text-2xl font-bold">Member dashboard</h1>
      {list.length === 0 ? (
        <EmptyState message="You are not in any stokvels yet." />
      ) : (
        <div className="grid gap-4 md:grid-cols-2">
          {list.map((s) => (
            <ContributionCard key={s.id} stokvel={s} />
          ))}
        </div>
      )}
    </div>
  );
}