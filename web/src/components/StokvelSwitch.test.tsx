import { describe, it, expect, afterEach, beforeEach, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { QueryClientProvider } from '@tanstack/react-query';
import { createQueryClient } from '../lib/queryClient';
import { Dashboard } from './Dashboard';
import { formatCentsAsRand } from '../lib/rand';
import { __resetSessionForTests } from '../auth/session';
import { clearSelectedStokvel } from '../state/selectedStokvel';

const json = (data: unknown) =>
  new Response(JSON.stringify(data), { status: 200, headers: { 'Content-Type': 'application/json' } });
const sleep = (ms: number) => new Promise((r) => setTimeout(r, ms));

beforeEach(() => {
  __resetSessionForTests();
  clearSelectedStokvel();
  // Fake API matching my real routes. Stokvel "b" answers SLOWLY (150 ms) on every call.
  // If it answered instantly the bug could not appear and the test would prove nothing.
  vi.stubGlobal('fetch', async (input: RequestInfo | URL) => {
    const path = new URL(input instanceof Request ? input.url : String(input)).pathname;
    if (path.endsWith('/auth/refresh')) return json({ accessToken: 'tok' });
    if (path.endsWith('/me/home')) return json({ stokvels: [{ stokvelId: 'a', name: 'Mabena Family' }, { stokvelId: 'b', name: 'Soweto Teachers' }] });
    if (path.endsWith('/api/v1/stokvels')) {
      return json([{ id: 'a', name: 'Mabena Family' }, { id: 'b', name: 'Soweto Teachers' }]);
    }
    const match = path.match(/\/api\/v1\/stokvels\/([^/]+)(?:\/cycles(?:\/([^/]+)\/contributions)?)?$/);
    if (!match) return new Response(null, { status: 404 });
    const [, id, cycleId] = match;
    if (id === 'b') await sleep(150);

    if (cycleId) {
      return json({
        items: [id === 'a'
          ? { id: 'a1', memberName: 'Thandi Mabena', amountCents: 11111, status: 'Paid' }
          : { id: 'b1', memberName: 'Sipho Dlamini', amountCents: 22222, status: 'Paid' }],
        nextPageToken: '',
      });
    }
    if (path.endsWith('/cycles')) {
      return json([{ id: `${id}-c1`, number: 1, status: 'Open' }]);
    }
    return json({ id, name: id === 'a' ? 'Mabena Family' : 'Soweto Teachers' });
  });
});

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe('stokvel switching', () => {
  it('never shows the previous stokvel amounts right after switching', async () => {
    render(
      <QueryClientProvider client={createQueryClient()}>
        <Dashboard />
      </QueryClientProvider>,
    );

    // stokvel A is on screen
    await screen.findByText(/Thandi Mabena/);
    expect(document.body.textContent).toContain(formatCentsAsRand(11111));

    // switch to B, then look IMMEDIATELY (B is still loading)
    fireEvent.change(screen.getByLabelText('Stokvel'), { target: { value: 'b' } });
    expect(document.body.textContent).not.toContain(formatCentsAsRand(11111));
    expect(document.body.textContent).not.toContain('Thandi Mabena');

    // later, B's own data arrives
    // Wait until B's heading shows; at that moment A's rows must NOT be under it
    await screen.findByRole('heading', { name: 'Soweto Teachers' });
    expect(document.body.textContent).not.toContain(formatCentsAsRand(11111));
    await screen.findByText(/Sipho Dlamini/);
    expect(document.body.textContent).toContain(formatCentsAsRand(22222));
  });
});
