'use server';

import { redirect } from 'next/navigation';
import { loginWithApi } from '@/lib/authApi';
import { loginMessageFor } from '@/lib/errorMessages';
import { safeNextPath } from '@/lib/safeNext';
import { createSession, deleteSession, getSession } from '@/lib/session';

export type SignInState = { error?: string; correlationId?: string; email?: string };

// Signs in. "next" is user input, so only a path on OUR site is followed (this keeps /join?code=... working after sign-in).
export async function signIn(_prev: SignInState, formData: FormData): Promise<SignInState> {
  const email = String(formData.get('email') ?? '').trim();
  const password = String(formData.get('password') ?? '');
  const next = safeNextPath(String(formData.get('next') ?? ''));
  if (!email || !password) return { email, error: 'Enter your email and password.' };

  const outcome = await loginWithApi(email, password);
  if (!outcome.ok) {
    return { email, error: loginMessageFor(outcome.status === 0 ? 500 : outcome.status), correlationId: outcome.correlationId };
  }
  await createSession(outcome.session);
  redirect(next);
}

export async function signOut(): Promise<void> {
  const session = await getSession();
  if (session) {
    try {
      await fetch(`${process.env.API_BASE_URL}/api/v1/auth/logout`, {
        method: 'POST',
        headers: { Cookie: `rondi_refresh=${session.refreshToken}` },
        cache: 'no-store',
      });
    } catch {
      // even if the API is down, we still delete the local session below
    }
  }
  await deleteSession();
  redirect('/login');
}