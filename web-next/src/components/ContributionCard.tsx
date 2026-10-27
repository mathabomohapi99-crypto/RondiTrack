// No 'use client' here: no state and no event handlers, so it stays a Server Component.
import { formatDate, formatMoney } from '@/lib/format';
import type { MyStokvel } from '@/lib/types';

export function ContributionCard({ stokvel }: { stokvel: MyStokvel }) {
  return (
    <div className="rounded-lg border p-4 shadow-sm">
      <h2 className="text-lg font-semibold">{stokvel.name}</h2>
      <p className="text-xs text-gray-500">Your role: {stokvel.myRole}</p>
      <dl className="mt-2 space-y-1 text-sm">
        <div>
          Paid this cycle: <strong>{formatMoney(stokvel.paidThisCycle)}</strong>
        </div>
        <div>
          Still owed: <strong>{formatMoney(stokvel.owedThisCycle)}</strong>
        </div>
        <div>
          Next payout: <strong>{formatDate(stokvel.nextPayoutDateUtc)}</strong>
        </div>
      </dl>
    </div>
  );
}