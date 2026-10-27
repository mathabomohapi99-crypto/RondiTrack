'use client'; // Needs 'use client' because the "Show details" toggle uses useState.

import Link from 'next/link';
import { useState } from 'react';
import { formatMoney } from '@/lib/format';
import type { MyStokvel } from '@/lib/types';


export function StokvelCard({ stokvel }: { stokvel: MyStokvel }) {
  const [open, setOpen] = useState(false);

  return (
    <div className="rounded-lg border p-4 shadow-sm">
      <div className="flex items-center justify-between">
        <Link href={`/stokvels/${stokvel.id}`} className="text-lg font-semibold underline">
          {stokvel.name}
        </Link>
        <span className="rounded bg-gray-100 px-2 py-1 text-xs text-black">{stokvel.myRole}</span>
      </div>
      <p className="mt-1 text-sm">
        {formatMoney(stokvel.contributionAmount)} {stokvel.frequency.toLowerCase()} · {stokvel.memberCount} members
      </p>
      <button type="button" className="mt-2 text-sm underline" onClick={() => setOpen(!open)}>
        {open ? 'Hide details' : 'Show details'}
      </button>
      {open && (
        <p className="mt-2 text-sm">
          Paid this cycle: {formatMoney(stokvel.paidThisCycle)} · Still owed: {formatMoney(stokvel.owedThisCycle)}
        </p>
      )}
    </div>
  );
}