import 'server-only';
import { correlationIdFrom } from './errors';
import { findRefreshToken } from './refresh';
import type { SessionData } from './sessionCrypto';

export type LoginOutcome =
  | { ok: true; session: SessionData }
  | { ok: false; status: number; correlationId?: string };

// Logs in at the API, asks who the person is, and returns the session to store. Shared by sign-in and register.
export async function loginWithApi(email: string, password: string): Promise<LoginOutcome> {
  const base = `${process.env.API_BASE_URL}/api/v1`;
  try {
    const res = await fetch(`${base}/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password }),
      cache: 'no-store',
    });
    if (!res.ok) {
      const body = await res.json().catch(() => null);
      return { ok: false, status: res.status, correlationId: correlationIdFrom(body) };
    }

    const body = (await res.json()) as { accessToken?: unknown; expiresAtUtc?: unknown };
    const refreshToken = findRefreshToken(res.headers.getSetCookie());
    if (typeof body.accessToken !== 'string' || !refreshToken) return { ok: false, status: 500 };

    const meRes = await fetch(`${base}/me`, {
      headers: { Authorization: `Bearer ${body.accessToken}` },
      cache: 'no-store',
    });
    if (!meRes.ok) {
      const meBody = await meRes.json().catch(() => null);
      return { ok: false, status: meRes.status, correlationId: correlationIdFrom(meBody) };
    }
    const me = (await meRes.json()) as { id: string; email: string; fullName: string };

    const expires = typeof body.expiresAtUtc === 'string' ? Date.parse(body.expiresAtUtc) : Number.NaN;
    return {
      ok: true,
      session: {
        accessToken: body.accessToken,
        accessExpiresAt: Number.isNaN(expires) ? Date.now() + 5 * 60_000 : expires,
        refreshToken,
        user: { id: me.id, email: me.email, displayName: me.fullName },
      },
    };
  } catch {
    return { ok: false, status: 0 }; // the API could not be reached
  }
}