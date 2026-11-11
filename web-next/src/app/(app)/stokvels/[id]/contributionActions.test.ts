// @vitest-environment node
import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';
import { server } from '@/test/server';

// The edges of the action: no real session, router or cache. The API is faked at the HTTP level by MSW.
vi.mock('server-only', () => ({}));
vi.mock('next/cache', () => ({ refresh: vi.fn() }));
vi.mock('next/navigation', () => ({
  redirect: (url: string) => { throw new Error(`NEXT_REDIRECT ${url}`); },
  notFound: () => { throw new Error('NEXT_NOT_FOUND'); },
}));
vi.mock('@/lib/session', () => ({
  getSession: async () => ({
    accessToken: 'test-token', accessExpiresAt: Date.now() + 600_000, refreshToken: 'r',
    user: { id: 'u', email: 'e@example.com', displayName: 'Test' },
  }),
}));

import { recordContribution } from './contributionActions';
import { initialFormState, type ContributionFormState } from './contributionTypes';

const STOKVEL = '11111111-1111-4111-8111-111111111111';
const CYCLE = '22222222-2222-4222-8222-222222222222';
const MEMBER = '33333333-3333-4333-8333-333333333333';
const K0 = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const URL = `http://api.test/api/v1/stokvels/:stokvelId/cycles/:cycleId/contributions`;

// What the browser would post: the hidden fields come from the previous answer, like in the real form
function formFor(prev: ContributionFormState, amount: string): FormData {
  const f = new FormData();
  f.set('stokvelId', STOKVEL);
  f.set('cycleId', CYCLE);
  f.set('memberId', MEMBER);
  f.set('amount', amount);
  f.set('idempotencyKey', prev.idempotencyKey);
  f.set('keyFor', prev.keyFor);
  return f;
}
const start = () => initialFormState(MEMBER, '150.50', K0);

describe('recordContribution: the Idempotency-Key (made here, on the server, so this is where it is tested)', () => {
  it('a dropped connection reuses the key, a success replaces it, and the next contribution carries a different one', async () => {
    const seen: string[] = [];
    let call = 0;
    server.use(http.post(URL, ({ request }) => {
      seen.push(request.headers.get('Idempotency-Key') ?? '');
      call++;
      return call === 1 ? HttpResponse.error() : HttpResponse.json({}, { status: 201 }); // 1st: dropped. Then: fine.
    }));

    const s0 = start();
    const s1 = await recordContribution(s0, formFor(s0, '150.50'));
    expect(s1.status).toBe('error');
    expect(s1.mayHaveLanded).toBe(true);

    const s2 = await recordContribution(s1, formFor(s1, '150.50')); // the retry
    expect(s2.status).toBe('success');
    expect(seen).toEqual([K0, K0]); // SAME key on the retry

    const s3 = await recordContribution(s2, formFor(s2, '200')); // the NEXT contribution
    expect(s3.status).toBe('success');
    expect(seen[2]).not.toBe(K0);
    expect(seen[2]).toBe(s2.idempotencyKey);
  });

  it('a changed amount after a failure is a different payment, so it gets a new key', async () => {
    const seen: string[] = [];
    let call = 0;
    server.use(http.post(URL, ({ request }) => {
      seen.push(request.headers.get('Idempotency-Key') ?? '');
      call++;
      return call === 1 ? HttpResponse.error() : HttpResponse.json({}, { status: 201 });
    }));

    const s0 = start();
    const s1 = await recordContribution(s0, formFor(s0, '150.50'));
    await recordContribution(s1, formFor(s1, '175'));
    expect(seen[0]).toBe(K0);
    expect(seen[1]).not.toBe(K0);
  });

  it('a 5xx keeps the key too (the outcome is unknown)', async () => {
    server.use(http.post(URL, () => HttpResponse.json({ correlationId: 'c1' }, { status: 503 })));
    const s0 = start();
    const s1 = await recordContribution(s0, formFor(s0, '150.50'));
    expect(s1.mayHaveLanded).toBe(true);
    expect(s1.idempotencyKey).toBe(K0);
    expect(s1.correlationId).toBe('c1');
  });
});

describe('recordContribution: every answer of the API', () => {
  it('maps the API field names to the form fields (amountInCents -> amount, userId -> memberId)', async () => {
    server.use(http.post(URL, () => HttpResponse.json(
      { errors: { amountInCents: ['Too much'], userId: ['Not a member'] }, correlationId: 'c2' }, { status: 400 })));
    const s0 = start();
    const s1 = await recordContribution(s0, formFor(s0, '150.50'));
    expect(s1.fieldErrors).toEqual({ amount: 'Too much', memberId: 'Not a member' });
    expect(s1.idempotencyKey).not.toBe(K0); // a real "no": the next try is a new payment
  });

  it('shows the API explanation and reference on a 409', async () => {
    server.use(http.post(URL, () => HttpResponse.json(
      { detail: 'This cycle is no longer open, so contributions cannot be recorded.', correlationId: 'c3' }, { status: 409 })));
    const s0 = start();
    const s1 = await recordContribution(s0, formFor(s0, '150.50'));
    expect(s1.message).toBe('This cycle is no longer open, so contributions cannot be recorded.');
    expect(s1.correlationId).toBe('c3');
  });

  it('refuses an invalid amount before any request is made', async () => {
    const s0 = start();
    const s1 = await recordContribution(s0, formFor(s0, '150.505')); // no handler: a request would fail the test
    expect(s1.fieldErrors?.amount).toMatch(/valid amount/);
  });
});