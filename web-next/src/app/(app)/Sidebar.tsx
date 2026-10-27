import Link from 'next/link';
import { redirect } from 'next/navigation';
import { getSession } from '@/lib/session';
import { signOut } from './actions';

// Server Component: it reads the session itself and only shows plain text, never the session object.
export async function Sidebar() {
  const session = await getSession();
  if (!session) redirect('/login');

  return (
    <aside className="flex w-60 flex-col gap-2 border-r p-4">
      <p className="text-sm">Signed in as</p>
      <p className="text-sm font-semibold">{session.user.displayName}</p>
      <p className="text-xs text-gray-500">{session.user.email}</p>
      {/* All four links show for everyone. Hiding "Treasurer" would only be a convenience. */}
      <nav className="mt-4 flex flex-col gap-1">
        <Link href="/" className="underline">Home</Link>
        <Link href="/stokvels" className="underline">My stokvels</Link>
        <Link href="/dashboard/member" className="underline">Member dashboard</Link>
        <Link href="/dashboard/treasurer" className="underline">Treasurer dashboard</Link>
      </nav>
      <form action={signOut} className="mt-4">
        <button type="submit" className="rounded border px-3 py-1 text-sm">Sign out</button>
      </form>
    </aside>
  );
}