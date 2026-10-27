'use server';

import { redirect } from 'next/navigation';
import { createSession } from '@/lib/session';
import { correlationIdFrom } from '@/lib/errors';
import { loginMessageFor, messageFor } from '@/lib/errorMessages';

export type SignInState = { error?: string; correlationId?: string };

// The name of the refresh cookie the API sets (AuthConstants.RefreshCookieName).
const REFRESH_COOKIE_NAME = 'rondi_refresh';

// Finds the refresh token value inside the Set-Cookie headers of the login response.
function findRefreshToken(setCookies: string[]): string | null {
  const header = setCookies.find((c) => c.startsWith(`${REFRESH_COOKIE_NAME}=`));
  if (!header) return null;
  return header.split(';')[0].slice(REFRESH_COOKIE_NAME.length + 1);
}

export async function signIn(_prev: SignInState, formData: FormData): Promise<SignInState> {
  const email = String(formData.get('email') ?? '');
  const password = String(formData.get('password') ?? '');
  const base = process.env.API_BASE_URL;

  // 1. Ask the API to log in.
  const res = await fetch(`${base}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
    cache: 'no-store',
  });

  // Wrong password etc: show the friendly message and the reference.
  if (!res.ok) {
    const body = await res.json().catch(() => null);
    return { error: loginMessageFor(res.status), correlationId: correlationIdFrom(body) };
  }

  const body = (await res.json()) as { accessToken?: unknown };
  const accessToken = body.accessToken;
  const refreshToken = findRefreshToken(res.headers.getSetCookie());
  if (typeof accessToken !== 'string' || !refreshToken) {
    return { error: messageFor(500) };
  }

  // 2. Ask the API who this is.
  const meRes = await fetch(`${base}/api/me`, {
    headers: { Authorization: `Bearer ${accessToken}` },
    cache: 'no-store',
  });
  if (!meRes.ok) {
    const meBody = await meRes.json().catch(() => null);
    return { error: messageFor(meRes.status), correlationId: correlationIdFrom(meBody) };
  }
  const me = (await meRes.json()) as { id: string; email: string; fullName: string };

  // 3. Keep everything in the encrypted cookie. No role is stored.
  await createSession({
    accessToken,
    refreshToken,
    user: { id: me.id, email: me.email, displayName: me.fullName },
  });

  // redirect() works by throwing, so it must stay outside any try/catch.
  redirect('/dashboard/member');
}