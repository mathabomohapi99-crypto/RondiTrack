import 'server-only'; // Build error if a Client Component ever imports this file.
import { cookies } from 'next/headers';
import { EncryptJWT, jwtDecrypt } from 'jose';

// Everything we keep about the signed-in person. NO role here: a role belongs to a person AND a stokvel,
// so it comes from the API for each stokvel.
export type SessionData = {
  accessToken: string;
  refreshToken: string;
  user: { id: string; email: string; displayName: string };
};

const COOKIE_NAME = 'session';
const MAX_AGE_SECONDS = 60 * 60 * 24 * 7; // 7 days (same as the API's refresh token)

// Reads the 32-byte secret from .env.local. Stops loudly if it is missing or the wrong size.
function getKey(): Uint8Array {
  const secret = process.env.SESSION_SECRET;
  if (!secret) throw new Error('SESSION_SECRET is not set');
  const key = Buffer.from(secret, 'base64');
  if (key.length !== 32) throw new Error('SESSION_SECRET must be 32 bytes, base64 encoded');
  return new Uint8Array(key);
}

// Checks that decrypted data really has the shape of SessionData.
function isSessionData(value: unknown): value is SessionData {
  if (!value || typeof value !== 'object') return false;
  const v = value as Record<string, unknown>;
  const u = v.user as Record<string, unknown> | undefined;
  return (
    typeof v.accessToken === 'string' &&
    typeof v.refreshToken === 'string' &&
    !!u &&
    typeof u.id === 'string' &&
    typeof u.email === 'string' &&
    typeof u.displayName === 'string'
  );
}

// Encrypts the data (dir + A256GCM) and stores it in the HttpOnly cookie.
export async function createSession(data: SessionData): Promise<void> {
  const jwt = await new EncryptJWT({ data })
    .setProtectedHeader({ alg: 'dir', enc: 'A256GCM' })
    .setIssuedAt()
    .setExpirationTime(`${MAX_AGE_SECONDS}s`)
    .encrypt(getKey());

  const store = await cookies();
  store.set('session', jwt, {
    httpOnly: true, // JavaScript in the browser cannot read it
    secure: true, // only sent over HTTPS (Chrome also allows it on localhost)
    sameSite: 'lax',
    path: '/',
    maxAge: MAX_AGE_SECONDS,
  });
}

// Reads the cookie. A bad cookie (cannot decrypt, wrong shape) counts as no session at all.
export async function getSession(): Promise<SessionData | null> {
  const store = await cookies();
  const token = store.get(COOKIE_NAME)?.value;
  if (!token) return null;
  try {
    const { payload } = await jwtDecrypt(token, getKey());
    return isSessionData(payload.data) ? payload.data : null;
  } catch {
    return null;
  }
}

// Deletes the cookie (used when signing out).
export async function deleteSession(): Promise<void> {
  const store = await cookies();
  store.delete(COOKIE_NAME);
}