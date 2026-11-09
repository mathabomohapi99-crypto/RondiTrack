'use client'; // useActionState, the file input's change event and the toasts

import { useActionState, useEffect, useRef, useState } from 'react';
import { useToasts } from '@/components/Toasts';
import { ALLOWED_PROOF_TYPES, checkProofFile } from '@/lib/proofRules';
import { messageFor } from '@/lib/errorMessages';
import { uploadProof } from './proofActions';
import type { ProofState } from './week9Types';

type Props = { stokvelId: string; contributionId: string; hasProof: boolean; label: string };

export function ProofUploadForm({ stokvelId, contributionId, hasProof, label }: Props) {
  const [state, formAction, pending] = useActionState(uploadProof, { status: 'idle' } as ProofState);
  const [clientError, setClientError] = useState<string | null>(null);
  const [changedSince, setChangedSince] = useState<ProofState | null>(null); // the person picked another file after the last answer
  const formRef = useRef<HTMLFormElement>(null);
  const toasts = useToasts();

  // Feedback: success = a toast that goes away. Unexpected failure = a toast that STAYS and shows the reference.
  useEffect(() => {
    if (state.status === 'success') {
      toasts.success(state.message ?? 'Proof of payment uploaded.');
      formRef.current?.reset();
    } else if (state.status === 'error' && state.unexpected) {
      toasts.error(state.message ?? messageFor(500), state.correlationId);
    }
  }, [state, toasts]);

  const errorId = `proof-error-${contributionId}`;
  const serverFileError = changedSince === state ? undefined : state.fileError;
  const fileError = clientError ?? serverFileError; // a wrong file is ALWAYS a message under the input

  return (
    <form
      ref={formRef}
      // The browser checks first: nothing is sent while the file is wrong
      action={(formData) => {
        if (clientError) return;
        formAction(formData);
      }}
      className="mt-2 space-y-1 text-sm"
    >
      <input type="hidden" name="stokvelId" value={stokvelId} />
      <input type="hidden" name="contributionId" value={contributionId} />
      <label htmlFor={`proof-${contributionId}`} className="block">
        {hasProof ? 'Replace the proof of payment' : 'Attach a proof of payment'} for {label}
      </label>
      <input
        id={`proof-${contributionId}`}
        name="file"
        type="file"
        accept={ALLOWED_PROOF_TYPES.join(',')}
        aria-invalid={fileError ? true : undefined}
        aria-describedby={fileError ? errorId : undefined}
        onChange={(e) => {
          const file = e.target.files?.[0];
          setClientError(file ? checkProofFile(file) : null);
          setChangedSince(state);
        }}
      />
      {fileError && <p id={errorId} role="alert" className="text-red-600">{fileError}</p>}
      {state.status === 'error' && !state.unexpected && state.message && (
        <p role="alert" className="text-red-600">
          {state.message}
          {state.correlationId && <span className="block text-xs text-gray-600">Reference: {state.correlationId}</span>}
        </p>
      )}
      <button type="submit" disabled={pending || clientError !== null} className="rounded border px-3 py-1 disabled:opacity-50">
        {pending ? 'Uploading…' : 'Upload'}
      </button>
    </form>
  );
}