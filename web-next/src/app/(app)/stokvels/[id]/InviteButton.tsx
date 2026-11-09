'use client'; // useActionState and the clipboard

import { useActionState, useState } from 'react';
import { formatDate } from '@/lib/format';
import { createInvite } from './inviteActions';
import type { InviteState } from './week9Types';

export function InviteButton({ stokvelId }: { stokvelId: string }) {
  const [state, formAction, pending] = useActionState(createInvite, {} as InviteState);
  const [copied, setCopied] = useState(false);

  async function copyLink(code: string) {
    await navigator.clipboard.writeText(`${window.location.origin}/join?code=${code}`);
    setCopied(true);
  }

  return (
    <form action={formAction} className="space-y-2 rounded border p-4">
      <h2 className="font-semibold">Invite someone</h2>
      <input type="hidden" name="stokvelId" value={stokvelId} />
      <button type="submit" disabled={pending} className="rounded border px-3 py-1 disabled:opacity-50">
        {pending ? 'Creating…' : 'Create invite code'}
      </button>
      <div role="status" className="text-sm">
        {state.code && (
          <>
            <p>Code: <strong className="font-mono">{state.code}</strong> (valid until {formatDate(state.expiresAtUtc)})</p>
            <button type="button" onClick={() => void copyLink(state.code!)} className="rounded border px-3 py-1">
              {copied ? 'Link copied' : 'Copy invite link'}
            </button>
          </>
        )}
      </div>
      {state.error && (
        <p role="alert" className="text-sm text-red-600">
          {state.error}{state.correlationId && ` (Reference: ${state.correlationId})`}
        </p>
      )}
    </form>
  );
}