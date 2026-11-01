import { Suspense } from 'react';
import { redirect } from 'next/navigation';
import { getSession } from '@/lib/session';

// The session is read inside <Suspense>, so the page itself never blocks.
async function Redirector() {
  const session = await getSession();
  redirect(session ? '/dashboard/member' : '/login');
}

// "/" only redirects: signed-in people to the member dashboard, everyone else to /login.
export default function Home() {
  return (
    <Suspense fallback={<p className="p-6 text-gray-600">Loading…</p>}>
      <Redirector />
    </Suspense>
  );
}