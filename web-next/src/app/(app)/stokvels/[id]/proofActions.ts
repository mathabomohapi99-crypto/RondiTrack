'use server';

import { refresh } from 'next/cache';
import { redirect } from 'next/navigation';
import { apiFetchWithToken } from '@/lib/api';
import { codeFrom, correlationIdFrom, detailFrom, fieldErrorsFrom } from '@/lib/errors';
import { messageFor } from '@/lib/errorMessages';
import { checkProofFile, MAX_PROOF_BYTES } from '@/lib/proofRules';
import { getSession } from '@/lib/session';
import type { ProofState } from './week9Types';

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

// A public endpoint: session first, then checks, then the API (which judges the file by its bytes).
export async function uploadProof(_prev: ProofState, formData: FormData): Promise<ProofState> {
  const session = await getSession();
  if (!session) redirect('/login?expired=1');

  const stokvelId = String(formData.get('stokvelId') ?? '');
  const contributionId = String(formData.get('contributionId') ?? '');
  if (!UUID.test(stokvelId) || !UUID.test(contributionId)) {
    return { status: 'error', unexpected: true, message: messageFor(400) };
  }

  const file = formData.get('file');
  if (!(file instanceof File) || file.size === 0) {
    return { status: 'error', fileError: 'Choose a photo or a PDF to upload.' };
  }
  // The same size rule as the browser. Between 5 and 6 MB the file still reaches here (Next.js allows 6 MB), so say it here too.
  if (file.size > MAX_PROOF_BYTES) {
    return { status: 'error', fileError: checkProofFile(file) ?? 'That file is too large.' };
  }

  // Forward the file to the API with the person's own token. No Content-Type: fetch sets the multipart boundary itself.
  const out = new FormData();
  out.set('file', file, file.name);

  let res: Response;
  try {
    res = await apiFetchWithToken(session.accessToken, `/stokvels/${stokvelId}/contributions/${contributionId}/proof`, {
      method: 'PUT',
      body: out,
    });
  } catch {
    return { status: 'error', unexpected: true, message: 'We could not reach RondiTrack, so the file was not uploaded. Please try again.' };
  }

  if (res.ok) {
    refresh(); // re-renders the page so the new proof shows up
    return { status: 'success', message: 'Proof of payment uploaded.' };
  }

  const body = await res.json().catch(() => null);
  const correlationId = correlationIdFrom(body);

  switch (res.status) {
    case 400:
    case 413: {
      // The API's message about the file goes in the SAME place as the browser's
      const f = fieldErrorsFrom(body);
      return { status: 'error', fileError: f.file ?? detailFrom(body) ?? 'That file could not be accepted.' };
    }
    case 401:
      redirect('/login?expired=1');
    case 404:
      return { status: 'error', message: "We couldn't find that contribution, or you can't add a proof to it.", correlationId };
    case 409:
      if (codeFrom(body) === 'proof_locked') refresh(); // the cycle just closed: the page stops offering the form
      return { status: 'error', message: detailFrom(body) ?? messageFor(409), correlationId };
    default:
      return res.status >= 500
        ? { status: 'error', unexpected: true, message: messageFor(500), correlationId }
        : { status: 'error', message: messageFor(res.status), correlationId };
  }
}