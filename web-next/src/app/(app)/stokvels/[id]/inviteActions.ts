'use server';

import { redirect } from 'next/navigation';
import { apiFetchWithToken } from '@/lib/api';
import { correlationIdFrom } from '@/lib/errors';
import { messageFor } from '@/lib/errorMessages';
import { getSession } from '@/lib/session';
import type { InviteState } from './week9Types';

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

// Makes a new invite code. The API decides who may (Treasurer or Admin of THIS stokvel).
export async function createInvite(_prev: InviteState, formData: FormData): Promise<InviteState> {
  const session = await getSession();
  if (!session) redirect('/login?expired=1');

  const stokvelId = String(formData.get('stokvelId') ?? '');
  if (!UUID.test(stokvelId)) return { error: messageFor(400) };

  let res: Response;
  try {
    res = await apiFetchWithToken(session.accessToken, `/stokvels/${stokvelId}/invites`, { method: 'POST' });
  } catch {
    return { error: messageFor(500) };
  }
  if (res.ok) {
    const body = (await res.json()) as { code: string; expiresAtUtc: string };
    return { code: body.code, expiresAtUtc: body.expiresAtUtc };
  }
  const body = await res.json().catch(() => null);
  if (res.status === 401) redirect('/login?expired=1');
  if (res.status === 403) return { error: 'Only a treasurer of this stokvel can create invites.', correlationId: correlationIdFrom(body) };
  return { error: messageFor(res.status), correlationId: correlationIdFrom(body) };
}