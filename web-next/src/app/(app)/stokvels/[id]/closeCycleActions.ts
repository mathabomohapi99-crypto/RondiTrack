'use server';

import { refresh } from 'next/cache';
import { redirect } from 'next/navigation';
import { apiFetchWithToken } from '@/lib/api';
import { codeFrom, correlationIdFrom, detailFrom } from '@/lib/errors';
import { messageFor } from '@/lib/errorMessages';
import { getSession } from '@/lib/session';
import type { CloseResult } from './week9Types';

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

// A public endpoint: session first. The version is the one the treasurer SAW; the API refuses it if it is stale.
export async function closeCycle(
  stokvelId: string, cycleId: string, version: string, idempotencyKey: string,
): Promise<CloseResult> {
  const session = await getSession();
  if (!session) redirect('/login?expired=1');

  if (!UUID.test(stokvelId) || !UUID.test(cycleId) || !/^[0-9a-f]{1,64}$/i.test(version) || !UUID.test(idempotencyKey)) {
    return { status: 'rejected', message: 'This request is not valid. Reload the page and try again.' };
  }

  let res: Response;
  try {
    res = await apiFetchWithToken(session.accessToken, `/stokvels/${stokvelId}/cycles/${cycleId}/close`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'Idempotency-Key': idempotencyKey },
      body: JSON.stringify({ version }),
    });
  } catch {
    return {
      status: 'unknown',
      message: 'We did not get an answer, so the cycle may have closed. Trying again is safe: it will not record arrears twice.',
    };
  }

  if (res.ok) {
    const body = (await res.json()) as { totalArrearsCents: number; arrearsRecorded: number };
    refresh(); // the page shows the closed cycle
    return { status: 'closed', totalArrearsCents: body.totalArrearsCents, arrearsRecorded: body.arrearsRecorded };
  }

  const body = await res.json().catch(() => null);
  const correlationId = correlationIdFrom(body);

  switch (res.status) {
    case 401:
      redirect('/login?expired=1');
    case 403:
      return { status: 'forbidden', correlationId }; // a message, NEVER a sign-out
    case 404:
      return { status: 'rejected', message: "We couldn't find that cycle.", correlationId };
    case 409: {
      const reason = codeFrom(body);
      if (reason === 'stale_preview') return { status: 'stale' };
      if (reason === 'cycle_closed') {
        refresh();
        return { status: 'already_closed' };
      }
      return { status: 'rejected', message: detailFrom(body) ?? messageFor(409), correlationId };
    }
    case 429: {
      const seconds = Number.parseInt(res.headers.get('Retry-After') ?? '', 10);
      const wait = Number.isFinite(seconds) ? `Try again in ${seconds} ${seconds === 1 ? 'second' : 'seconds'}.` : 'Please wait a moment.';
      return { status: 'unknown', message: `Too many requests. ${wait} Trying again is safe.`, correlationId };
    }
    default:
      return res.status >= 500
        ? { status: 'unknown', message: 'RondiTrack had a problem, so the cycle may have closed. Trying again is safe: it will not record arrears twice.', correlationId }
        : { status: 'rejected', message: messageFor(res.status), correlationId };
  }
}