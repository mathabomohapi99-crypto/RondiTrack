import { api, unwrap } from '@/lib/api';
import { StokvelCard } from '@/components/StokvelCard';
import { EmptyState } from '@/components/EmptyState';
import type { MyStokvel } from '@/lib/types';

// Every stokvel the person belongs to, with their role in each.
export default async function StokvelsPage() {
  const client = await api();
  const list = unwrap<MyStokvel[]>(await client.GET('/api/me/stokvels'));

  return (
    <div>
      <h1 className="mb-4 text-2xl font-bold">My stokvels</h1>
      {list.length === 0 ? (
        <EmptyState message="You are not in any stokvels yet." />
      ) : (
        <div className="grid gap-4 md:grid-cols-2">
          {list.map((s) => (
            <StokvelCard key={s.id} stokvel={s} />
          ))}
        </div>
      )}
    </div>
  );
}