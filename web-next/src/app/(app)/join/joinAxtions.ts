'use server';

import { redirect } from 'next/navigation';
import { apiFetchWithToken } from '@/lib/api';
import { codeFrom, correlationIdFrom } from '@/lib/errors';
import { messageFor } from '@/lib/errorMessages';
import { fetchInvitePreview } from '@/lib/invites';
import { joinHref, normalizeCode, type JoinState } from '@/lib/joinFlow';
import { getSession } from '@/lib/session';

// Step 1: check the code with the API, then move on. Every answer is a screen (a message on step 1), never an error page.
export async function checkCode(formData: FormData): Promise<void> {
  const raw = String(formData.get('code') ?? '').trim().slice(0, 40);
  const code = normalizeCode(raw);
  if (!code) redirect(joinHref('code', raw || null, 'bad_format'));

  const session = await getSession();
  if (!session) redirect(`/login?next=${encodeURIComponent(joinHref('code', code))}`);

  const outcome = await fetchInvitePreview(session.accessToken, code);
  if (outcome.kind === 'signedOut') redirect('/login?expired=1');
  if (outcome.kind === 'problem') redirect(joinHref('code', code, outcome.problem));
  redirect(joinHref(outcome.preview.alreadyMember ? 'member' : 'rules', code));
}

// Step 3: join, with the person's OWN token. The API makes a second join impossible, and a second join lands in the stokvel too.
export async function joinStokvel(_prev: JoinState, formData: FormData): Promise<JoinState> {
  const session = await getSession();
  if (!session) redirect('/login?expired=1');

  const code = normalizeCode(String(formData.get('code') ?? ''));
  if (!code) redirect(joinHref('code', null, 'bad_format'));

  let res: Response;
  try {
    res = await apiFetchWithToken(session.accessToken, `/invites/${code}/join`, { method: 'POST' });
  } catch {
    return { error: 'We did not get an answer, so you may already have joined. Trying again is safe: you will not join twice.' };
  }

  if (res.ok) {
    const body = (await res.json().catch(() => null)) as { stokvelId?: string } | null;
    redirect(body?.stokvelId ? `/stokvels/${body.stokvelId}` : '/stokvels');
  }

  const body = await res.json().catch(() => null);
  const correlationId = correlationIdFrom(body);

  switch (res.status) {
    case 401:
      redirect('/login?expired=1');
    case 404:
      redirect(joinHref('code', code, 'not_found'));
    case 410:
      redirect(joinHref('code', code, 'expired'));
    case 409: {
      const reason = codeFrom(body);
      if (reason === 'already_member') {
        // A double-click or a retry after a dropped connection: the first join worked, so go to the stokvel
        const outcome = await fetchInvitePreview(session.accessToken, code);
        if (outcome.kind === 'ok' && outcome.preview.stokvelId) redirect(`/stokvels/${outcome.preview.stokvelId}`);
        redirect(joinHref('member', code));
      }
      if (reason === 'stokvel_full') return { error: 'This stokvel is full. Ask its treasurer to make room.', correlationId };
      return { error: messageFor(409), correlationId };
    }
    case 429:
      return { error: 'Too many attempts. Please wait a minute and try again.', correlationId };
    default:
      return { error: messageFor(res.status), correlationId };
  }
}