'use client'; // useActionState is a React hook

import { useActionState } from 'react';
import type { JoinState } from '@/lib/joinFlow';
import { joinStokvel } from './joinActions';

// Step 3 button. One click = one join: disabled while the request runs (and the API refuses a second membership anyway).
export function JoinForm({ code, stokvelName }: { code: string; stokvelName: string }) {
  const [state, formAction, pending] = useActionState(joinStokvel, {} as JoinState);

  return (
    <form action={formAction} className="space-y-3">
      <input type="hidden" name="code" value={code} />
      {state.error && (
        <p role="alert" className="rounded border border-red-300 p-3 text-sm">
          {state.error}
          {state.correlationId && <span className="block text-xs text-gray-600">Reference: {state.correlationId}</span>}
        </p>
      )}
      <button type="submit" disabled={pending} className="rounded bg-black px-4 py-2 text-white disabled:opacity-50">
        {pending ? 'Joining…' : `Join ${stokvelName}`}
      </button>
    </form>
  );
}