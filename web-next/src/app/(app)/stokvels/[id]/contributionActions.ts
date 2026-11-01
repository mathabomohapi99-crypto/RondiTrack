'use server';

import { refresh } from 'next/cache';
import { redirect } from 'next/navigation';
import { z } from 'zod';
import { apiFetchWithToken } from '@/lib/api';
import { correlationIdFrom, detailFrom, fieldErrorsFrom } from '@/lib/errors';
import { messageFor } from '@/lib/errorMessages';
import { formatCents } from '@/lib/format';
import { parseRandsToCents } from '@/lib/rands';
import { getSession } from '@/lib/session';
import type { ContributionFormState } from './contributionTypes';

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

// The 7.4 Zod schema, now running on the server: OUR parser, so "150.505" is refused instead of rounded
const schema = z.object({
  memberId: z.string().regex(UUID, 'Choose a member'),
  amount: z
    .string()
    .refine((v) => parseRandsToCents(v) !== null, 'Enter a valid amount, like 150.50 (at most 2 decimals)')
    .refine((v) => (parseRandsToCents(v) ?? 0) > 0, 'The amount must be more than R0'),
});

// A public endpoint: a hidden form proves nothing. Order: session, schema, then the API.
export async function recordContribution(
  prev: ContributionFormState,
  formData: FormData,
): Promise<ContributionFormState> {
  // 1. session first
  const session = await getSession();
  if (!session) redirect('/login?expired=1');

  const stokvelId = String(formData.get('stokvelId') ?? '');
  const cycleId = String(formData.get('cycleId') ?? '');
  const memberId = String(formData.get('memberId') ?? '');
  const amountText = String(formData.get('amount') ?? '');
  const sentKey = String(formData.get('idempotencyKey') ?? '');
  const keyFor = String(formData.get('keyFor') ?? '');
  const values = { memberId, amount: amountText };

  // 2. schema
  const parsed = schema.safeParse({ memberId, amount: amountText });
  if (!parsed.success || !UUID.test(stokvelId) || !UUID.test(cycleId)) {
    const fieldErrors: { memberId?: string; amount?: string } = {};
    if (!parsed.success) {
      for (const issue of parsed.error.issues) {
        const field = issue.path[0];
        if ((field === 'memberId' || field === 'amount') && !fieldErrors[field]) fieldErrors[field] = issue.message;
      }
    }
    return {
      ...prev, // no request was made, so the key stays as it was
      status: 'error',
      message: Object.keys(fieldErrors).length > 0 ? 'Please check the highlighted fields.' : 'This form is not valid. Reload the page and try again.',
      correlationId: undefined,
      fieldErrors,
      values,
    };
  }

  const amountInCents = parseRandsToCents(amountText)!; // the schema just proved it is valid

  // The 7.4 key table, decided HERE on the server:
  //   same member + amount as the attempt that made this key  -> REUSE the key (a retry)
  //   member or amount changed since then                     -> NEW key
  const fingerprint = `${stokvelId}|${cycleId}|${memberId}|${amountInCents}`;
  const keyIsReusable = UUID.test(sentKey) && (keyFor === '' || keyFor === fingerprint);
  const key = keyIsReusable ? sentKey : crypto.randomUUID();

  // Outcome unknown (no answer, 429, 5xx): keep the key and say so
  const keepKey = (message: string, correlationId?: string): ContributionFormState => ({
    status: 'error', message, correlationId, values, idempotencyKey: key, keyFor: fingerprint, mayHaveLanded: true,
  });
  // A real "no" (400/403/404/409/422): nothing was recorded, so the next try is a fresh payment with a NEW key
  const rejected = (
    message: string,
    correlationId?: string,
    fieldErrors?: { memberId?: string; amount?: string },
  ): ContributionFormState => ({
    status: 'error', message, correlationId, fieldErrors, values,
    idempotencyKey: crypto.randomUUID(), keyFor: '', mayHaveLanded: false,
  });

  // 3. the API
  let res: Response;
  try {
    res = await apiFetchWithToken(session.accessToken, `/stokvels/${stokvelId}/cycles/${cycleId}/contributions`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'Idempotency-Key': key },
      body: JSON.stringify({ userId: memberId, amountInCents }),
    });
  } catch {
    return keepKey(
      'We did not get an answer, so your payment may have gone through. Your details are kept. Trying again is safe: it will not pay twice.',
    );
  }

  if (res.ok) {
    // WITHOUT this line the action's answer arrives but the page keeps its old total (see the README).
    // The total is not cached under a tag, so refresh() is the right call: it re-renders this page in this response.
    refresh();
    return {
      status: 'success',
      message: `Recorded ${formatCents(amountInCents)}.`,
      values: { memberId, amount: '' },
      idempotencyKey: crypto.randomUUID(), // a success ends this intent: the next payment gets a NEW key
      keyFor: '',
    };
  }

  const body = await res.json().catch(() => null);
  const correlationId = correlationIdFrom(body);

  switch (res.status) {
    case 400: {
      const f = fieldErrorsFrom(body);
      return rejected(messageFor(400), correlationId, { amount: f.amountincents, memberId: f.userid });
    }
    case 401:
      redirect('/login?expired=1'); // the session is over (never shown as "no access")
    case 403:
      // Never signs anyone out. The form stays usable.
      return rejected('You are not allowed to record a contribution for another member in this stokvel.', correlationId);
    case 404:
      return rejected("We couldn't find that stokvel or cycle.", correlationId);
    case 409:
    case 422:
      return rejected(detailFrom(body) ?? messageFor(res.status), correlationId); // the API's own explanation
    case 429: {
      const seconds = Number.parseInt(res.headers.get('Retry-After') ?? '', 10);
      const wait = Number.isFinite(seconds) ? `Try again in ${seconds} ${seconds === 1 ? 'second' : 'seconds'}.` : 'Please wait a moment.';
      return keepKey(`Too many requests. ${wait} Your details are kept, and trying again is safe.`, correlationId);
    }
    default:
      if (res.status >= 500) {
        return keepKey(
          'RondiTrack had a problem, so your payment may have gone through. Your details are kept. Trying again is safe: it will not pay twice.',
          correlationId,
        );
      }
      return rejected(messageFor(res.status), correlationId);
  }
}