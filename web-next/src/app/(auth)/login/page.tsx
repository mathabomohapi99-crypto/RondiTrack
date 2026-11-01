import Link from 'next/link';
import { Suspense } from 'react';
import { ClearPinsOnLogin } from '@/components/ClearPinsOnLogin';
import { LoginForm } from './LoginForm';

type SearchParams = PageProps<'/login'>['searchParams'];

// This page does NOT redirect signed-in people: an expired token still has a cookie,
// and redirecting would loop with /login?expired=1.
export default function LoginPage({ searchParams }: PageProps<'/login'>) {
  return (
    <main className="mx-auto mt-24 max-w-sm p-4">
      <ClearPinsOnLogin />
      <h1 className="text-2xl font-bold">Sign in to RondiTrack</h1>
      {/* searchParams is read inside <Suspense> */}
      <Suspense fallback={<p className="mt-4 text-sm text-gray-600">Loading…</p>}>
        <LoginBody searchParams={searchParams} />
      </Suspense>
    </main>
  );
}

async function LoginBody({ searchParams }: { searchParams: SearchParams }) {
  const sp = await searchParams; // searchParams is a Promise in Next 16
  const next = typeof sp.next === 'string' ? sp.next : '';
  return (
    <>
      {sp.expired === '1' && <p className="mt-2 text-sm text-amber-700">Your session ended. Please sign in again.</p>}
      <LoginForm next={next} />
      <p className="mt-4 text-sm">
        No account yet?{' '}
        <Link href="/register" className="underline">Create one</Link>
      </p>
    </>
  );
}