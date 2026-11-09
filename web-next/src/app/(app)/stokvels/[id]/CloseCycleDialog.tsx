'use client'; // a native <dialog>, fetch and state

import { useId, useRef, useState } from 'react';
import { RowsSkeleton } from '@/components/Skeletons';
import { useToasts } from '@/components/Toasts';
import { formatCents, formatDate } from '@/lib/format';
import { closeCycle } from './closeCycleActions';
import type { ClosePreview } from './week9Types';

type Props = { stokvelId: string; cycleId: string; cycleNumber: number };
type View =
  | { kind: 'loading' }
  | { kind: 'ready'; preview: ClosePreview }
  | { kind: 'failed'; message: string; correlationId?: string };
type Notice = { message: string; correlationId?: string };

function previewMessage(status: number): string {
  if (status === 403) return 'Only a treasurer of this stokvel can close a cycle.';
  if (status === 404) return "We couldn't find that cycle.";
  if (status === 409) return 'This cycle is already closed.';
  return 'We could not load what closing will do. Please try again.';
}

export function CloseCycleDialog({ stokvelId, cycleId, cycleNumber }: Props) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const cancelRef = useRef<HTMLButtonElement>(null);
  const keyRef = useRef(''); // the Idempotency-Key of THIS intent
  const [view, setView] = useState<View>({ kind: 'loading' });
  const [notice, setNotice] = useState<Notice | null>(null);
  const [closing, setClosing] = useState(false);
  const toasts = useToasts();
  const titleId = useId();
  const bodyId = useId();

  // Asks the API what closing will do. A NEW preview is a new intent, so it gets a NEW key.
  async function loadPreview() {
    setView({ kind: 'loading' });
    try {
      const res = await fetch(`/bff/close-preview?stokvelId=${stokvelId}&cycleId=${cycleId}`, {
        headers: { Accept: 'application/json' },
        cache: 'no-store',
      });
      if (res.status === 401) {
        window.location.assign('/login?expired=1');
        return;
      }
      const body = await res.json().catch(() => null);
      if (!res.ok) {
        setView({ kind: 'failed', message: previewMessage(res.status), correlationId: body?.correlationId });
        return;
      }
      keyRef.current = crypto.randomUUID();
      setView({ kind: 'ready', preview: body as ClosePreview });
    } catch {
      setView({ kind: 'failed', message: 'We could not reach RondiTrack. Check your connection and try again.' });
    }
  }

  function open() {
    setNotice(null);
    dialogRef.current?.showModal(); // native modal: Esc closes it and focus goes back to the button that opened it
    cancelRef.current?.focus(); // focus starts on Cancel, never on the destructive button
    void loadPreview();
  }

  async function confirm() {
    if (view.kind !== 'ready' || closing) return;
    const { preview } = view;
    setClosing(true);
    setNotice(null);

    const result = await closeCycle(stokvelId, cycleId, preview.version, keyRef.current).catch(
      () => ({
        status: 'unknown' as const,
        message: 'We did not get an answer, so the cycle may have closed. Trying again is safe: it will not record arrears twice.',
      }),
    );
    setClosing(false);

    switch (result.status) {
      case 'closed':
        dialogRef.current?.close();
        toasts.success(
          `Closed Cycle ${preview.cycleNumber}. ${formatCents(result.totalArrearsCents)} in arrears recorded for ${result.arrearsRecorded} ${result.arrearsRecorded === 1 ? 'member' : 'members'}.`,
        );
        break;
      case 'stale':
        // What you saw is no longer true: nothing was recorded. Show the NEW numbers and ask again.
        setNotice({ message: 'Something changed since you opened this. Check the new numbers below, then confirm again.' });
        await loadPreview();
        break;
      case 'already_closed':
        setView({ kind: 'failed', message: 'This cycle is already closed.' });
        break;
      case 'forbidden':
        setNotice({ message: 'You are not a treasurer of this stokvel, so you cannot close its cycles.', correlationId: result.correlationId });
        break;
      case 'unknown': // no real answer: KEEP the key, so a retry cannot record twice
        setNotice({ message: result.message, correlationId: result.correlationId });
        break;
      case 'rejected': // a real "no": nothing was recorded, so the next try is a fresh intent
        keyRef.current = crypto.randomUUID();
        setNotice({ message: result.message, correlationId: result.correlationId });
        break;
    }
  }

  const ready = view.kind === 'ready' ? view.preview : null;
  const label = closing
    ? 'Closing…'
    : ready
      ? ready.totalArrearsCents > 0
        ? `Close Cycle ${cycleNumber} and record ${formatCents(ready.totalArrearsCents)} in arrears`
        : `Close Cycle ${cycleNumber} (nobody is in arrears)`
      : `Close Cycle ${cycleNumber}`;

  return (
    <>
      <button type="button" onClick={open} className="rounded border border-red-600 px-4 py-2 text-red-700">
        Close Cycle {cycleNumber}…
      </button>

      <dialog
        ref={dialogRef}
        role="alertdialog"
        aria-labelledby={titleId}
        aria-describedby={bodyId}
        onCancel={(e) => {
          if (closing) e.preventDefault(); // Esc does not close it while the close is running
        }}
        className="m-auto w-full max-w-lg rounded bg-white p-6 text-black backdrop:bg-black/50"
      >
        <h2 id={titleId} className="text-lg font-bold">Close Cycle {cycleNumber}?</h2>

        <div id={bodyId} aria-busy={view.kind === 'loading'} className="mt-3 space-y-3 text-sm">
          {notice && (
            <p role="alert" className="rounded border border-amber-500 p-3">
              {notice.message}
              {notice.correlationId && <span className="block text-xs text-gray-600">Reference: {notice.correlationId}</span>}
            </p>
          )}

          {view.kind === 'loading' && <RowsSkeleton rows={3} />}

          {view.kind === 'failed' && (
            <p role="alert" className="rounded border border-red-300 p-3">
              {view.message}
              {view.correlationId && <span className="block text-xs text-gray-600">Reference: {view.correlationId}</span>}
            </p>
          )}

          {ready && (
            <>
              <p>
                As of {formatDate(`${ready.asOfDate}T12:00:00Z`)}, closing Cycle {ready.cycleNumber} cannot be undone.
              </p>
              {ready.arrears.length === 0 ? (
                <p>Nobody is in arrears. Everyone has paid in full.</p>
              ) : (
                <>
                  <p>These members will be recorded in arrears:</p>
                  <ul className="space-y-1">
                    {ready.arrears.map((a) => (
                      <li key={a.userId} className="flex justify-between border-b py-1">
                        <span>{a.memberName}</span>
                        <span>{formatCents(a.shortfallCents)}</span>
                      </li>
                    ))}
                  </ul>
                  <p className="font-semibold">Total in arrears: {formatCents(ready.totalArrearsCents)}</p>
                </>
              )}
            </>
          )}
        </div>

        <div className="mt-5 flex flex-wrap justify-end gap-2">
          <button ref={cancelRef} type="button" disabled={closing} onClick={() => dialogRef.current?.close()}
            className="rounded border px-4 py-2 disabled:opacity-50">
            Cancel
          </button>
          {view.kind === 'failed' && (
            <button type="button" onClick={() => void loadPreview()} className="rounded border px-4 py-2">Try again</button>
          )}
          {/* Disabled until there is something to confirm, and while the close runs */}
          <button type="button" onClick={() => void confirm()} disabled={view.kind !== 'ready' || closing}
            className="rounded bg-red-700 px-4 py-2 text-white disabled:opacity-50">
            {label}
          </button>
        </div>
      </dialog>
    </>
  );
}