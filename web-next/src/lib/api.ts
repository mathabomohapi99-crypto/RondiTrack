import 'server-only'; // Only the server may build API calls with the token.
import createClient from 'openapi-fetch';
import { notFound, redirect } from 'next/navigation';
import type { paths } from '@/types/schema';
import { getSession } from './session';
import { ApiError, correlationIdFrom } from './errors';

// A typed API client that adds "Authorization: Bearer ..." from the session. No session = go to /login.
export async function api() {
  const session = await getSession();
  if (!session) redirect('/login');

  return createClient<paths>({
    baseUrl: process.env.API_BASE_URL,
    headers: { Authorization: `Bearer ${session.accessToken}` },
  });
}

// Turns a result into data, or into the right response:
// 401 -> /login?expired=1, 404 -> not-found page, anything else -> ApiError (friendly message + reference).
// T is the shape I expect (from src/lib/types.ts).
export function unwrap<T>(result: { data?: unknown; error?: unknown; response: Response }): T {
  const { data, error, response } = result;
  if (response.status === 401) redirect('/login?expired=1');
  if (response.status === 404) notFound();
  if (!response.ok || data === undefined) {
    throw new ApiError(response.status, correlationIdFrom(error));
  }
  return data as T;
}