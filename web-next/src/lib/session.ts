import 'server-only'; // Build error if a Client Component ever imports this file.
import { cookies } from 'next/headers';
import {
  decryptSession,
  encryptSession,
  SESSION_COOKIE,
  sessionCookieOptions,
  type SessionData,
} from './sessionCrypto';

export type { SessionData };

// Encrypts the data and stores it in the HttpOnly cookie (used by sign-in and register)
export async function createSession(data: SessionData): Promise<void> {
  const jwt = await encryptSession(data);
  const store = await cookies();
  store.set(SESSION_COOKIE, jwt, sessionCookieOptions);
}

// Reads the cookie. proxy.ts may have refreshed it just before this request.
export async function getSession(): Promise<SessionData | null> {
  const store = await cookies();
  const token = store.get(SESSION_COOKIE)?.value;
  if (!token) return null;
  return decryptSession(token);
}

// Deletes the cookie (sign out)
export async function deleteSession(): Promise<void> {
  const store = await cookies();
  store.delete(SESSION_COOKIE);
}