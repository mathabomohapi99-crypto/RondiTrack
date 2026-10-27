// @vitest-environment node
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { authFetch, login, onSignedOut, __resetSessionForTests } from './session';

const BASE = 'http://localhost:5134';

type Call = { auth: string | null; body: string };
let fake: {
  refreshCount: number;
  refreshOk: boolean;
  validToken: string; // the only token the fake API accepts
  nextToken: string; // what /auth/refresh hands out
  calls: Call[]; // every non-auth request the fake saw
};

const json = (data: unknown) =>
  new Response(JSON.stringify(data), { status: 200, headers: { 'Content-Type': 'application/json' } });

beforeEach(() => {
  __resetSessionForTests(); // fresh module state for every test
  fake = { refreshCount: 0, refreshOk: true, validToken: 'new', nextToken: 'new', calls: [] };

  // Fake API. openapi-fetch passes a Request object, so we handle Request AND string URLs.
  vi.stubGlobal('fetch', async (input: RequestInfo | URL, init?: RequestInit) => {
    const isReq = input instanceof Request;
    const url = isReq ? input.url : String(input);
    const headers = isReq ? input.headers : new Headers(init?.headers);

    if (url.endsWith('/auth/login')) return json({ accessToken: 'old' });
    if (url.endsWith('/auth/refresh')) {
      fake.refreshCount++;
      await new Promise((r) => setTimeout(r, 20)); // delay so the race can really happen
      if (!fake.refreshOk) return new Response(null, { status: 401 });
      return json({ accessToken: fake.nextToken });
    }

    const body = isReq ? await input.clone().text() : String(init?.body ?? '');
    const auth = headers.get('Authorization');
    fake.calls.push({ auth, body });
    return auth === `Bearer ${fake.validToken}` ? json({ ok: true }) : new Response(null, { status: 401 });
  });
});

afterEach(() => {
  vi.unstubAllGlobals();
});

const get = () => authFetch(new Request(`${BASE}/api/v1/stokvels`));

describe('session', () => {
  it('five parallel 401s cause exactly one refresh', async () => {
    await login('a@b.c', 'pw'); // token "old" is not accepted by the fake
    const responses = await Promise.all([get(), get(), get(), get(), get()]);
    expect(fake.refreshCount).toBe(1);
    expect(responses.map((r) => r.status)).toEqual([200, 200, 200, 200, 200]);
  });

  it('a later expiry starts a new refresh', async () => {
    await login('a@b.c', 'pw');
    await Promise.all([get(), get(), get(), get(), get()]);
    expect(fake.refreshCount).toBe(1);

    // token "expires": the fake now only accepts "newer"
    fake.validToken = 'newer';
    fake.nextToken = 'newer';
    const responses = await Promise.all([get(), get()]);
    expect(fake.refreshCount).toBe(2); // the first promise was cleared
    expect(responses.map((r) => r.status)).toEqual([200, 200]);
  });

  it('a failed refresh signs out once', async () => {
    fake.refreshOk = false;
    const signedOut = vi.fn();
    onSignedOut(signedOut);
    await login('a@b.c', 'pw');
    const responses = await Promise.all([get(), get()]);
    expect(signedOut).toHaveBeenCalledTimes(1);
    expect(fake.refreshCount).toBe(1);
    expect(fake.calls).toHaveLength(2); // each request sent once, never retried
    expect(responses.map((r) => r.status)).toEqual([401, 401]);
  });

  it('no token means no loop', async () => {
    fake.refreshOk = false; // fresh visitor: no cookie
    const res = await get();
    expect(fake.refreshCount).toBe(1); // one refresh attempt
    expect(fake.calls).toHaveLength(1); // one request
    expect(res.status).toBe(401); // comes straight back
  });

  it('a POST body survives the retry', async () => {
    await login('a@b.c', 'pw');
    const payload = JSON.stringify({ amount: 500 });
    const res = await authFetch(
      new Request(`${BASE}/api/v1/contributions`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: payload,
      }),
    );
    expect(res.status).toBe(200);
    expect(fake.calls).toHaveLength(2); // first try 401, retry 200
    expect(fake.calls[0].body).toBe(payload);
    expect(fake.calls[1].body).toBe(payload); // same body on the retry
  });
});