'use client'; // Needs 'use client': the details toggle uses useState and the pin button reads the browser store.

import Link from 'next/link';
import { useState } from 'react';
import { formatMoney } from '@/lib/format';
import type { MyStokvel } from '@/lib/types';
import { usePins, usePinsStoreApi } from './PinsProvider';

export function StokvelCard({ stokvel }: { stokvel: MyStokvel }) {
  const [open, setOpen] = useState(false);
  const store = usePinsStoreApi();
  const pinned = usePins((s) => s.pins.some((p) => p.id === stokvel.id));

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
      <div className="mt-2 flex gap-4">
        <button type="button" className="inline-flex min-h-6 items-center px-1 text-sm underline"" onClick={() => setOpen(!open)}>
          {open ? 'Hide details' : 'Show details'}
        </button>
        {/* A pin stores ONLY the id and the name */}
        <button
          type="button"
          className="inline-flex min-h-6 items-center px-1 text-sm underline"          aria-pressed={pinned}
          onClick={() => store.getState().togglePin({ id: stokvel.id, name: stokvel.name })}
        >
          {pinned ? 'Unpin' : 'Pin'}
        </button>
      </div>
      {open && (
        <p className="mt-2 text-sm">
          Paid this cycle: {formatMoney(stokvel.paidThisCycle)} · Still owed: {formatMoney(stokvel.owedThisCycle)}
        </p>
      )}
    </div>
  );
}