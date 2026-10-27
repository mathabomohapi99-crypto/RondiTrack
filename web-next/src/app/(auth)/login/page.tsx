import { LoginForm } from './LoginForm';

// This page does NOT redirect signed-in people: an expired token still has a cookie,
// and redirecting would loop with /login?expired=1.
export default async function LoginPage({ searchParams }: PageProps<'/login'>) {
  const { expired } = await searchParams; // searchParams is a Promise in Next 16
  return (
    <main className="mx-auto mt-24 max-w-sm p-4">
      <h1 className="text-2xl font-bold">Sign in to RondiTrack</h1>
      {expired === '1' && <p className="mt-2 text-sm text-amber-700">Your session ended. Please sign in again.</p>}
      <LoginForm />
    </main>
  );
}