import { api, unwrap } from '@/lib/api';
import { formatDate, formatMoney } from '@/lib/format';
import type { StokvelDetail } from '@/lib/types';

// A non-member, a missing stokvel and a malformed id all give the API's 404, and unwrap() shows not-found.tsx.
export default async function StokvelDetailPage({ params }: PageProps<'/stokvels/[id]'>) {
  const { id } = await params; // params is a Promise in Next 16

  const client = await api();
  const stokvel = unwrap<StokvelDetail>(
    await client.GET('/api/stokvels/{stokvelId}', { params: { path: { stokvelId: id } } }),
  );
  const cycle = stokvel.currentCycle;

  return (
    <div className="space-y-3">
      <h1 className="text-2xl font-bold">{stokvel.name}</h1>
      <p>Contribution: {formatMoney(stokvel.contributionAmount)} ({stokvel.frequency.toLowerCase()})</p>
      <p>Your role here: {stokvel.myRole ?? 'Not a member (platform admin view)'}</p>
      <p>Members: {stokvel.memberCount} of {stokvel.maxMembers}</p>
      <section className="rounded border p-4">
        <h2 className="font-semibold">Current cycle</h2>
        {cycle ? (
          <ul className="mt-1 space-y-1 text-sm">
            <li>Cycle {cycle.cycleNumber}, started {formatDate(cycle.startedAtUtc)}</li>
            <li>Collected: {formatMoney(cycle.collectedAmount)} of {formatMoney(cycle.targetAmount)}</li>
            <li>Next payout: {formatDate(cycle.nextPayoutDateUtc)}</li>
          </ul>
        ) : (
          <p className="text-sm">No open cycle right now.</p>
        )}
      </section>
    </div>
  );
}