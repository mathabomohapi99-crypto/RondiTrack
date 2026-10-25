/*
 * WHY THE REFRESH MUST BE SINGLE-FLIGHT FOR MY API:
 * My API rotates the refresh cookie, so every refresh uses up the cookie it was sent with.
 * If five requests expire together and each sends its own refresh with the SAME old cookie,
 * the first wins and the other four are rejected ([FILL IN: the status you saw in check 5,
 * e.g. 401 or 409]), which would sign the member out. One shared refresh avoids that.
 *
 * This is the ONLY file in the project that reads or writes the access token.
 */
import { apiErrorFromResponse } from '../api/apiError';
import { API_BASE_URL } from '../api/config';

// Token lives only in this variable (never in browser storage, JS-set cookies, or React state)
let accessToken: string | null = null;
// The refresh that is currently running (shared by everyone who asks at the same time)
let refreshPromise: Promise<string | null> | null = null;
const signedOutListeners = new Set<() => void>();

// The rest of the app subscribes here to hear "member signed out"
export function onSignedOut(listener: () => void): () => void {
  signedOutListeners.add(listener);
  return () => {
    signedOutListeners.delete(listener);
  };
}

function notifySignedOut() {
  signedOutListeners.forEach((l) => l());
}

// Reads accessToken out of a JSON body without hand-writing a type
function tokenFrom(body: unknown): string | null {
  if (body && typeof body === 'object' && 'accessToken' in body && typeof body.accessToken === 'string') {
    return body.accessToken;
  }
  return null;
}

export async function login(email: string, password: string): Promise<void> {
  const res = await fetch(`${API_BASE_URL}/api/v1/auth/login`, {
    method: 'POST',
    credentials: 'include', // so the browser stores the refresh cookie
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  });
  if (!res.ok) throw await apiErrorFromResponse(res);
  const token = tokenFrom(await res.json());
  if (!token) throw new Error('Login response had no access token');
  accessToken = token;
}

export async function logout(): Promise<void> {
  const tokenBefore = accessToken;
  accessToken = null; // forget the token first, no matter what happens next
  try {
    await fetch(`${API_BASE_URL}/api/v1/auth/logout`, {
      method: 'POST',
      credentials: 'include',
      headers: tokenBefore ? { Authorization: `Bearer ${tokenBefore}` } : {},
    });
  } catch {
    // network failed: we still sign out locally
  } finally {
    notifySignedOut(); // always tell the app
  }
}

// The real refresh call. Success stores the token; failure clears it and signs out.
async function doRefresh(): Promise<string | null> {
  try {
    const res = await fetch(`${API_BASE_URL}/api/v1/auth/refresh`, {
      method: 'POST',
      credentials: 'include',
    });
    const token = res.ok ? tokenFrom(await res.json()) : null;
    if (!token) throw new Error('refresh failed');
    accessToken = token;
    return token;
  } catch {
    accessToken = null;
    notifySignedOut();
    return null;
  }
}

export function refreshAccessToken(): Promise<string | null> {
  // SINGLE-FLIGHT: if a refresh is already running, everyone gets that same promise.
  // .finally clears it (success OR failure) so the next expiry can start a new refresh.
  refreshPromise ??= doRefresh().finally(() => {
    refreshPromise = null;
  });
  return refreshPromise;
}

// Sends a copy of the request with the token and cookie settings added
function send(req: Request, token: string | null): Promise<Response> {
  const headers = new Headers(req.headers);
  if (token) headers.set('Authorization', `Bearer ${token}`);
  return fetch(new Request(req, { headers, credentials: 'include' }));
}

// The fetch we hand to openapi-fetch
export async function authFetch(request: Request): Promise<Response> {
  // A body can only be read once, so keep a clone for a possible retry
  const retryCopy = request.clone();

  // After a page reload memory is empty: get a token using the cookie first
  if (!accessToken) await refreshAccessToken();

  const tokenUsed = accessToken;
  const response = await send(request, tokenUsed);

  // Not a 401, or we had NO token to begin with: return now (this stops the endless loop)
  if (response.status !== 401 || !tokenUsed) return response;

  // If another request already got a newer token, use it instead of refreshing again
  const freshToken =
    accessToken && accessToken !== tokenUsed ? accessToken : await refreshAccessToken();

  if (!freshToken) return response; // refresh failed, doRefresh already signed us out
  return send(retryCopy, freshToken); // retry exactly once
}

// ONLY for tests: wipes module state between tests
export function __resetSessionForTests() {
  accessToken = null;
  refreshPromise = null;
  signedOutListeners.clear();
}

// NEW 7.4: the signed-in user's id, read from the "sub" claim of the access token.
// It lives here because this is the ONLY file allowed to touch the token. Returns null when signed out.
export function currentUserId(): string | null {
  if (!accessToken) return null;
  try {
    const payload = accessToken.split('.')[1];
    const parsed: unknown = JSON.parse(atob(payload.replace(/-/g, '+').replace(/_/g, '/')));
    if (parsed && typeof parsed === 'object' && 'sub' in parsed && typeof parsed.sub === 'string') {
      return parsed.sub;
    }
    return null;
  } catch {
    return null;
  }
}
