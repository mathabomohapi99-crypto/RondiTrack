import Link from 'next/link';
import { api, unwrap } from '@/lib/api';
import { EmptyState } from '@/components/EmptyState';
import { stokvelsWhereTreasurer } from '@/lib/stokvelsWhereTreasurer';
import type { MyStokvel } from '@/lib/types';

// A FILTERED VIEW, not a door with a role check. Everyone may open it.
// Most people will see the empty state, and that is correct.
export default async function TreasurerDashboardPage() {
  const client = await api();
  const list = unwrap<MyStokvel[]>(await client.GET('/api/me/stokvels'));
  const mine = stokvelsWhereTreasurer(list); // the role comes from the API's answer for each stokvel

  return (
    <div>
      <h1 className="mb-4 text-2xl font-bold">Treasurer dashboard</h1>
      {mine.length === 0 ? (
        <EmptyState
          message="You don't run any stokvels yet."
          linkHref="/dashboard/member"
          linkText="Go to your member dashboard"
        />
      ) : (
        <ul className="space-y-2">
          {mine.map((s) => (
            <li key={s.id}>
              <Link href={`/stokvels/${s.id}`} className="underline">
                {s.name}
              </Link>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}