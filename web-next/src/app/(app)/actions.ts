'use server';

import { redirect } from 'next/navigation';
import { deleteSession, getSession } from '@/lib/session';

// Tells the API to log out (with the refresh token), deletes our cookie, goes to /login.
export async function signOut(): Promise<void> {
  const session = await getSession();
  if (session) {
    try {
      // The API reads the refresh token from the "rondi_refresh" cookie on this request.
      await fetch(`${process.env.API_BASE_URL}/api/auth/logout`, {
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