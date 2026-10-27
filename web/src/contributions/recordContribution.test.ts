// @vitest-environment node
// NEW 7.4: the real generated client + authFetch + key logic run. ONLY the network is fake (MSW).
import { http, HttpResponse } from 'msw';
import { setupServer } from 'msw/node';
import { afterAll, afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../api/apiError';
import { API_BASE_URL } from '../api/config';
import { __resetSessionForTests, login } from '../auth/session';
import { parseRetryAfter } from '../lib/errorMessages';
import { parseRandsToCents } from '../lib/rands';
import { createIntentKeeper } from './intentKey';
import { createContributionSubmitter } from './submitContribution';

const CONTRIBUTIONS = `${API_BASE_URL}/api/v1/stokvels/:stokvelId/cycles/:cycleId/contributions`;
const recorded = { id: 'c1', stokvelId: 's1', userId: 'm1', cycleId: 'cy1', amount: 150.5, recordedAtUtc: '2026-10-25T10:00:00Z' };

// What the fake server saw on each request to the contributions endpoint
let keysSeen: (string | null)[] = [];
let authSeen: (string | null)[] = [];
let bodiesSeen: unknown[] = [];
let attempt = 0;

const server = setupServer(
  // the real login() runs against this
  http.post(`${API_BASE_URL}/api/v1/auth/login`, () => HttpResponse.json({ accessToken: 'token-1' })),
  // authFetch's refresh runs against this
  http.post(`${API_BASE_URL}/api/v1/auth/refresh`, () => HttpResponse.json({ accessToken: 'token-2' })),
);

beforeAll(() => server.listen({ onUnhandledRequest: 'error' })); // a request nobody expected fails the test
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

beforeEach(async () => {
  keysSeen = [];
  authSeen = [];
  bodiesSeen = [];
  attempt = 0;
  __resetSessionForTests();
  await login('treasurer@test.local', 'password'); // real login, fake network
});

// Records what arrived, then lets each test choose the response for attempt n
function contributionsHandler(respond: (n: number) => Response) {
  return http.post(CONTRIBUTIONS, async ({ request }) => {
    keysSeen.push(request.headers.get('Idempotency-Key'));
    authSeen.push(request.headers.get('Authorization'));
    bodiesSeen.push(await request.clone().json());
    attempt++;
    return respond(attempt);
  });
}

const created = () => HttpResponse.json(recorded, { status: 201 });
const input = { stokvelId: 's1', cycleId: 'cy1', userId: 'm1', amountInCents: 15050 };

describe('recording a contribution', () => {
  it('1. a retry after a network error sends the same key', async () => {
    // First attempt never gets an answer: a dropped connection
    server.use(contributionsHandler((n) => (n === 1 ? HttpResponse.error() : created())));
    const submit = createContributionSubmitter();

    await expect(submit(input)).rejects.toBeInstanceOf(TypeError);
    await submit(input); // the person clicks "Try again", nothing changed

    expect(keysSeen).toHaveLength(2);
    expect(keysSeen[0]).not.toBeNull();
    expect(keysSeen[1]).not.toBeNull();
    expect(keysSeen[0]).toBe(keysSeen[1]);
  });

  it('2. the next contribution gets a new key', async () => {
    server.use(contributionsHandler(() => created()));
    const submit = createContributionSubmitter();

    await submit(input);
    await submit(input); // a second, new payment

    expect(keysSeen).toHaveLength(2);
    expect(keysSeen[0]).not.toBe(keysSeen[1]);
  });

  it('3. a 429 keeps the key and exposes the wait', async () => {
    server.use(
      contributionsHandler((n) =>
        n === 1
          ? HttpResponse.json({ title: 'Too many requests' }, { status: 429, headers: { 'Retry-After': '1' } })
          : created(),
      ),
    );
    const submit = createContributionSubmitter();

    const error = await submit(input).catch((e: unknown) => e);
    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(429);
    expect(parseRetryAfter((error as ApiError).retryAfter)).toBe(1); // the form shows this wait

    await submit(input); // after the wait
    expect(keysSeen[0]).not.toBeNull();
    expect(keysSeen[0]).toBe(keysSeen[1]);
  });

  it('4. changing the amount after a failure is a new intent', async () => {
    server.use(contributionsHandler((n) => (n === 1 ? HttpResponse.error() : created())));
    const submit = createContributionSubmitter();

    await expect(submit(input)).rejects.toBeDefined();
    await submit({ ...input, amountInCents: 20000 }); // the person corrected the amount

    expect(keysSeen[0]).not.toBe(keysSeen[1]);
    expect(bodiesSeen[1]).toMatchObject({ amountInCents: 20000 });
  });

  it('5. a 401 retry keeps the key and uses the new token', async () => {
    server.use(
      contributionsHandler((n) => (n === 1 ? new HttpResponse(null, { status: 401 }) : created())),
    );
    const submit = createContributionSubmitter();

    await submit(input); // authFetch refreshes and retries by itself

    expect(keysSeen).toHaveLength(2);
    expect(keysSeen[0]).not.toBeNull();
    expect(keysSeen[1]).toBe(keysSeen[0]);
    expect(authSeen[0]).toBe('Bearer token-1');
    expect(authSeen[1]).toBe('Bearer token-2'); // the new token
  });

  it('6. cents survive the trip: "150.50" is sent as the number 15050', async () => {
    server.use(contributionsHandler(() => created()));
    const submit = createContributionSubmitter();

    await submit({ ...input, amountInCents: parseRandsToCents('150.50')! });

    const body = bodiesSeen[0] as { amountInCents: unknown };
    expect(body.amountInCents).toBe(15050);
    expect(typeof body.amountInCents).toBe('number');
  });
});

describe('StrictMode safety', () => {
  it('no key is made until someone submits, so a double render cannot make two', () => {
    const makeKey = vi.fn(() => 'k1');
    const keeper = createIntentKeeper(makeKey);
    expect(makeKey).not.toHaveBeenCalled(); // creating the keeper (what StrictMode runs twice) makes no key
    keeper.keyFor('a');
    keeper.keyFor('a');
    expect(makeKey).toHaveBeenCalledTimes(1); // one intent, one key
  });
});