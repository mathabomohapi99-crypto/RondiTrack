'use server';

import { redirect } from 'next/navigation';
import { loginWithApi } from '@/lib/authApi';
import { correlationIdFrom, fieldErrorsFrom } from '@/lib/errors';
import { messageFor } from '@/lib/errorMessages';
import { createSession } from '@/lib/session';

export type RegisterState = {
  error?: string;
  correlationId?: string;
  fieldErrors?: { fullname?: string; email?: string; password?: string };
  values?: { fullName: string; email: string }; // kept after a failure (never the password)
};

export async function register(_prev: RegisterState, formData: FormData): Promise<RegisterState> {
  const fullName = String(formData.get('fullName') ?? '').trim();
  const email = String(formData.get('email') ?? '').trim();
  const password = String(formData.get('password') ?? '');
  const values = { fullName, email };

  // The API checks this too. We check first so the person gets the message without a round trip.
  if (password.length < 12) {
    return { values, fieldErrors: { password: 'Password must be at least 12 characters.' } };
  }

  let res: Response;
  try {
    res = await fetch(`${process.env.API_BASE_URL}/api/v1/auth/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ fullName, email, password }),
      cache: 'no-store',
    });
  } catch {
    return { values, error: messageFor(500) };
  }

  if (!res.ok) {
    const body = await res.json().catch(() => null);
    const correlationId = correlationIdFrom(body);
    if (res.status === 400) {
      const f = fieldErrorsFrom(body);
      return {
        values,
        correlationId,
        fieldErrors: { fullname: f.fullname, email: f.email, password: f.password },
        error: Object.keys(f).length === 0 ? messageFor(400) : undefined,
      };
    }
    if (res.status === 409) {
      // SAME words whether the email exists or not, so this page never tells anyone who uses RondiTrack
      return {
        values,
        correlationId,
        error: "We couldn't create an account with those details. If you already have an account, sign in instead.",
      };
    }
    return { values, correlationId, error: messageFor(res.status) };
  }

  // Registration worked: sign the person in straight away
  const outcome = await loginWithApi(email, password);
  if (!outcome.ok) redirect('/login'); // account exists but sign-in failed: send them to sign in
  await createSession(outcome.session);
  redirect('/dashboard/member');
}