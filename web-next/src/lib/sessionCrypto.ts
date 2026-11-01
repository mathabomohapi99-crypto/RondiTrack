// Encrypt / decrypt the session. NO 'server-only' here on purpose: proxy.ts needs it too.
import { EncryptJWT, jwtDecrypt } from 'jose';

// Everything we keep about the signed-in person. NO role: a role belongs to a person AND a stokvel.
export type SessionData = {
  accessToken: string;
  accessExpiresAt: number; // when the access token ends (milliseconds since 1970)
  refreshToken: string;
  user: { id: string; email: string; displayName: string };
};

export const SESSION_COOKIE = 'session';
export const SESSION_MAX_AGE_SECONDS = 60 * 60 * 24 * 7; // 7 days = the API's refresh token life

// The cookie flags, shared by login (cookies().set) and proxy.ts (response.cookies.set)
export const sessionCookieOptions = {
  httpOnly: true, // JavaScript in the browser cannot read it
  secure: true,
  sameSite: 'lax' as const,
  path: '/',
  maxAge: SESSION_MAX_AGE_SECONDS,
};

function getKey(): Uint8Array {
  const secret = process.env.SESSION_SECRET;
  if (!secret) throw new Error('SESSION_SECRET is not set');
  const key = Buffer.from(secret, 'base64');
  if (key.length !== 32) throw new Error('SESSION_SECRET must be 32 bytes, base64 encoded');
  return new Uint8Array(key);
}

// Checks that decrypted data really has the shape of SessionData
function isSessionData(value: unknown): value is SessionData {
  if (!value || typeof value !== 'object') return false;
  const v = value as Record<string, unknown>;
  const u = v.user as Record<string, unknown> | undefined;
  return (
    typeof v.accessToken === 'string' &&
    typeof v.accessExpiresAt === 'number' &&
    typeof v.refreshToken === 'string' &&
    !!u &&
    typeof u.id === 'string' &&
    typeof u.email === 'string' &&
    typeof u.displayName === 'string'
  );
}

export async function encryptSession(data: SessionData): Promise<string> {
  return new EncryptJWT({ data })
    .setProtectedHeader({ alg: 'dir', enc: 'A256GCM' })
    .setIssuedAt()
    .setExpirationTime(`${SESSION_MAX_AGE_SECONDS}s`)
    .encrypt(getKey());
}

// A bad or old cookie counts as no session at all
export async function decryptSession(token: string): Promise<SessionData | null> {
  try {
    const { payload } = await jwtDecrypt(token, getKey());
    return isSessionData(payload.data) ? payload.data : null;
  } catch {
    return null;
  }
}