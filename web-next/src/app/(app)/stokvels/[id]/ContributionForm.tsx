'use client'; // useActionState is a React hook.

import { useQueryClient } from '@tanstack/react-query';
import { useActionState, useEffect } from 'react';
import { recordContribution } from './contributionActions';
import { initialFormState, type MemberOption } from './contributionTypes';

type Props = {
  stokvelId: string;
  cycleId: string;
  cycleNumber: number;
  meId: string;
  members: MemberOption[]; // only filled when the picker shows
  showPicker: boolean; // decided on the server from THIS stokvel's role
  defaultAmount: string;
  initialKey: string; // the first Idempotency-Key, written by the server
};

export function ContributionForm(props: Props) {
  const queryClient = useQueryClient();
  const [state, formAction, pending] = useActionState(
    recordContribution,
    initialFormState(props.meId, props.defaultAmount, props.initialKey),
  );

  // The server refreshed page one. TanStack Query still holds LATER pages of the history and feed
  // from before this payment, so those are dropped: the next visit loads them fresh.
  useEffect(() => {
    if (state.status === 'success') {
      queryClient.removeQueries({ queryKey: ['contributions'] });
      queryClient.removeQueries({ queryKey: ['activity'] });
    }
  }, [state, queryClient]);

  return (
    <form action={formAction} className="space-y-3 rounded border p-4">
      <h2 className="font-semibold">Record a contribution (Cycle {props.cycleNumber})</h2>

      <input type="hidden" name="stokvelId" value={props.stokvelId} />
      <input type="hidden" name="cycleId" value={props.cycleId} />
      {/* The Idempotency-Key is a hidden field the SERVER writes, not browser state */}
      <input type="hidden" name="idempotencyKey" value={state.idempotencyKey} />
      <input type="hidden" name="keyFor" value={state.keyFor} />

      {state.status === 'error' && state.message && (
        <div role="alert" className="rounded border border-red-300 p-3 text-sm">
          <p>{state.message}</p>
          {state.correlationId && <p className="mt-1 text-xs text-gray-500">Reference: {state.correlationId}</p>}
        </div>
      )}
      {state.status === 'success' && state.message && (
        <p role="status" className="text-sm text-green-700">{state.message}</p>
      )}

           {props.showPicker ? (
        <div>
          <label htmlFor="memberId" className="block text-sm">Member</label>
          <select id="memberId" name="memberId" defaultValue={state.values.memberId}
            aria-invalid={state.fieldErrors?.memberId ? true : undefined}
            aria-describedby={state.fieldErrors?.memberId ? 'memberId-error' : undefined}
            className="rounded border p-2">
            {props.members.map((m) => (
              <option key={m.id} value={m.id}>{m.name}</option>
            ))}
          </select>
          {state.fieldErrors?.memberId && <p id="memberId-error" role="alert" className="text-sm text-red-600">{state.fieldErrors.memberId}</p>}
        </div>
      ) : (
        <input type="hidden" name="memberId" value={props.meId} />
      )}

      <div>
        <label htmlFor="amount" className="block text-sm">Amount (rands)</label>
        <input id="amount" name="amount" inputMode="decimal" autoComplete="off"
          defaultValue={state.values.amount}
          aria-invalid={state.fieldErrors?.amount ? true : undefined}
          aria-describedby={state.fieldErrors?.amount ? 'amount-error' : undefined}
          className="rounded border p-2" />
        {state.fieldErrors?.amount && <p id="amount-error" role="alert" className="text-sm text-red-600">{state.fieldErrors.amount}</p>}
      </div>

      {/* One click, one request: disabled while pending */}
      <button type="submit" disabled={pending} className="rounded bg-black px-4 py-2 text-white disabled:opacity-50">
        {pending ? 'Recording…' : state.mayHaveLanded ? 'Try again' : 'Record contribution'}
      </button>
    </form>
  );
}