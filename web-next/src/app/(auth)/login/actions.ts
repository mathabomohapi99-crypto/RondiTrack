'use server';

import { redirect } from 'next/navigation';
import { deleteSession, getSession } from '@/lib/session';

// Sign out stays a POST (a form submits a Server Action). It tells the API to revoke the refresh token,
// ALWAYS deletes our cookie, then goes to /login, where the browser's pins are wiped.
export async function signOut(): Promise<void> {
  const session = await getSession();
  if (session) {
    try {
      // CHANGED 8.2: /api/v1 path. The API reads the refresh token from the "rondi_refresh" cookie.
      await fetch(`${process.env.API_BASE_URL}/api/v1/auth/logout`, {
        method: 'POST',
        headers: { Cookie: `rondi_refresh=${session.refreshToken}` },
        cache: 'no-store',
      });
    } catch {
      // Even if the API is down, we still delete the local session below.
    }
  }
  await deleteSession();
  redirect('/login');
}