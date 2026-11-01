import Link from 'next/link';
import { redirect } from 'next/navigation';
import { PinnedStokvels } from '@/components/PinnedStokvels';
import { apiFetchWithToken } from '@/lib/api';
import { getSession } from '@/lib/session';
import type { MyStokvel } from '@/lib/types';
import { signOut } from './actions';

// The ids of my stokvels, joined by commas, so pins for stokvels I left can be dropped. null = could not load.
async function myStokvelIdsKey(accessToken: string): Promise<string | null> {
  try {
    const res = await apiFetchWithToken(accessToken, '/me/stokvels');
    if (!res.ok) return null;
    return ((await res.json()) as MyStokvel[]).map((s) => s.id).join(',');
  } catch {
    return null;
  }
}

// Server Component: it reads the session itself and only passes plain text and ids on.
export async function Sidebar() {
  const session = await getSession();
  if (!session) redirect('/login');
  const idsKey = await myStokvelIdsKey(session.accessToken);

  return (
    <aside className="flex w-60 flex-col gap-2 border-r p-4">
      <p className="text-sm">Signed in as</p>
      <p className="text-sm font-semibold">{session.user.displayName}</p>
      <p className="text-xs text-gray-500">{session.user.email}</p>
      {/* All links show for everyone. Hiding "Treasurer" would only be a convenience. */}
      <nav className="mt-4 flex flex-col gap-1">
        <Link href="/" className="underline">Home</Link>
        <Link href="/stokvels" className="underline">My stokvels</Link>
        <Link href="/dashboard/member" className="underline">Member dashboard</Link>
        <Link href="/dashboard/treasurer" className="underline">Treasurer dashboard</Link>
        <Link href="/activity" className="underline">Activity</Link>
      </nav>
      <PinnedStokvels userId={session.user.id} stokvelIdsKey={idsKey} />
      <form action={signOut} className="mt-4">
        <button type="submit" className="rounded border px-3 py-1 text-sm">Sign out</button>
      </form>
    </aside>
  );
}