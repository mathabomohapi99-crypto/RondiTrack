// The ONLY code that calls the API's refresh endpoint. Used only by proxy.ts.
// Many requests with the same refresh token at once must share ONE refresh,
// otherwise the API sees the same token twice and revokes the whole session (token theft rule).
import { createHash } from 'node:crypto';
import type { SessionData } from './sessionCrypto';

export const REFRESH_COOKIE_NAME = 'rondi_refresh';

// Finds the refresh token value inside the Set-Cookie headers of a login/refresh response
export function findRefreshToken(setCookies: string[]): string | null {
  const header = setCookies.find((c) => c.startsWith(`${REFRESH_COOKIE_NAME}=`));
  if (!header) return null;
  return header.split(';')[0].slice(REFRESH_COOKIE_NAME.length + 1);
}

export type RefreshResult =
  | { kind: 'ok'; session: SessionData }
  | { kind: 'refused' } // the API said 401: the session is over
  | { kind: 'unavailable' }; // API down or odd answer: keep the session, try again later

// How long a finished refresh may be reused by requests that left before it came back
const REUSE_WINDOW_MS = 30_000;

type State = {
  running: Map<string, Promise<RefreshResult>>;
  finished: Map<string, { at: number; result: RefreshResult }>;
};
// On globalThis so dev-mode reloads do not lose it. Keys are hashes, never the raw token.
const g = globalThis as typeof globalThis & { __rondiRefresh?: State };
const state: State = (g.__rondiRefresh ??= { running: new Map(), finished: new Map() });

const keyOf = (token: string) => createHash('sha256').update(token).digest('hex');
// Logs time + a short user id + what happened. Never a token.
const log = (session: SessionData, message: string) =>
  console.log(`[refresh] ${new Date().toISOString()} user=${session.user.id.slice(0, 8)} ${message}`);

async function callApi(session: SessionData): Promise<RefreshResult> {
  let res: Response;
  try {
    res = await fetch(`${process.env.API_BASE_URL}/api/v1/auth/refresh`, {
      method: 'POST',
      headers: { Cookie: `${REFRESH_COOKIE_NAME}=${session.refreshToken}` },
      cache: 'no-store',
    });
  } catch {
    return { kind: 'unavailable' };
  }
  if (res.status === 401) return { kind: 'refused' };
  if (!res.ok) return { kind: 'unavailable' };

  const body = (await res.json().catch(() => null)) as { accessToken?: unknown; expiresAtUtc?: unknown } | null;
  const refreshToken = findRefreshToken(res.headers.getSetCookie());
  if (!body || typeof body.accessToken !== 'string' || !refreshToken) return { kind: 'unavailable' };

  const expires = typeof body.expiresAtUtc === 'string' ? Date.parse(body.expiresAtUtc) : Number.NaN;
  return {
    kind: 'ok',
    session: {
      ...session,
      accessToken: body.accessToken,
      refreshToken,
      accessExpiresAt: Number.isNaN(expires) ? Date.now() + 5 * 60_000 : expires,
    },
  };
}

export async function refreshOnce(session: SessionData): Promise<RefreshResult> {
  const key = keyOf(session.refreshToken);
  const now = Date.now();
  for (const [k, v] of state.finished) if (now - v.at > REUSE_WINDOW_MS) state.finished.delete(k);

  // 1. someone just finished this exact refresh: reuse its answer
  const done = state.finished.get(key);
  if (done) {
    log(session, 'reused the result of a refresh that just finished');
    return done.result;
  }
  // 2. someone is refreshing right now: wait for the same answer
  const running = state.running.get(key);
  if (running) {
    log(session, 'joined a refresh that is already running');
    return running;
  }
  // 3. we are first: call the API once
  log(session, 'calling the API to refresh');
  const promise = callApi(session)
    .then((result) => {
      if (result.kind !== 'unavailable') state.finished.set(key, { at: Date.now(), result });
      log(session, `refresh answer: ${result.kind}`);
      return result;
    })
    .finally(() => state.running.delete(key));
  state.running.set(key, promise);
  return promise;
}