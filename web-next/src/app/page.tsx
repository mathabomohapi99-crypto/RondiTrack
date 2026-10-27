import { redirect } from 'next/navigation';
import { getSession } from '@/lib/session';

// "/" sends signed-in people to the member dashboard and everyone else to /login.
export default async function Home() {
  const session = await getSession();
  redirect(session ? '/dashboard/member' : '/login');
}