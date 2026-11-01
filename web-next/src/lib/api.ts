import 'server-only'; // Only the server may build API calls with the token.
import { notFound, redirect } from 'next/navigation';
import { getSession } from './session';
import { ApiError, correlationIdFrom } from './errors';

// CHANGED 8.2: plain fetch against /api/v1 (the old typed client pointed at /api/...).
const base = () => `${process.env.API_BASE_URL}/api/v1`;

// Low level: call the API with a token you already have (used by Server Actions and Route Handlers,
// which must NOT redirect).
export async function apiFetchWithToken(accessToken: string, path: string, init: RequestInit = {}): Promise<Response> {
  const headers = new Headers(init.headers);
  headers.set('Authorization', `Bearer ${accessToken}`);
  return fetch(`${base()}${path}`, { ...init, headers, cache: 'no-store' });
}

// Same, but reads the token from the session. No session = go to /login.
export async function apiFetch(path: string, init: RequestInit = {}): Promise<Response> {
  const session = await getSession();
  if (!session) redirect('/login');
  return apiFetchWithToken(session.accessToken, path, init);
}

// For pages: data, or the right response.
// 401 -> /login?expired=1, 404 -> not-found page, anything else -> ApiError (friendly message + reference).
export async function apiJson<T>(path: string): Promise<T> {
  const res = await apiFetch(path);
  if (res.status === 401) redirect('/login?expired=1');
  if (res.status === 404) notFound();
  if (!res.ok) {
    const body = await res.json().catch(() => null);
    throw new ApiError(res.status, correlationIdFrom(body));
  }
  return (await res.json()) as T;
}