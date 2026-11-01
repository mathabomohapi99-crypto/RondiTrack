'use client'; // nuqs hooks read and write the URL in the browser.

import { useQueryStates } from 'nuqs';
import { useTransition } from 'react';
import { contributionParsers } from '@/lib/contributionSearchParams';
import type { MemberOption } from '@/lib/types';

export function ContributionFilters({ members }: { members: MemberOption[] }) {
  // isPending is true while the server renders page one again
  const [isPending, startTransition] = useTransition();

  const [filters, setFilters] = useQueryStates(contributionParsers, {
    shallow: false, // ask the server to render the list again (page one comes from the URL)
    history: 'replace', // a filter tweak replaces the URL: Back leaves the page instead of stepping through every tweak
    clearOnDefault: true, // defaults leave the URL, so the unfiltered page keeps a clean address
    startTransition,
  });

  return (
    <div className="space-y-2" aria-busy={isPending}>
      <div className="flex flex-wrap items-end gap-3">
        <div>
          <label htmlFor="member" className="block text-sm">Member</label>
          <select id="member" className="rounded border p-2" value={filters.member}
            onChange={(e) => void setFilters({ member: e.target.value })}>
            <option value="">Everyone</option>
            {members.map((m) => (
              <option key={m.id} value={m.id}>{m.name}</option>
            ))}
          </select>
        </div>
        <div>
          <label htmlFor="from" className="block text-sm">From</label>
          <input id="from" type="date" className="rounded border p-2" value={filters.from}
            onChange={(e) => void setFilters({ from: e.target.value })} />
        </div>
        <div>
          <label htmlFor="to" className="block text-sm">To (included)</label>
          <input id="to" type="date" className="rounded border p-2" value={filters.to}
            onChange={(e) => void setFilters({ to: e.target.value })} />
        </div>
        <div>
          <label htmlFor="sort" className="block text-sm">Sort</label>
          <select id="sort" className="rounded border p-2" value={filters.sort}
            onChange={(e) => void setFilters({ sort: e.target.value as typeof filters.sort })}>
            <option value="-date">Newest first</option>
            <option value="date">Oldest first</option>
            <option value="-amount">Biggest first</option>
            <option value="amount">Smallest first</option>
          </select>
        </div>
        <button type="button" className="rounded border px-3 py-2 text-sm" onClick={() => void setFilters(null)}>
          Clear filters
        </button>
      </div>
      <p role="status" className="text-sm text-gray-600">{isPending ? 'Updating the list…' : ''}</p>
    </div>
  );
}