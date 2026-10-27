import { useEffect, useState } from 'react';
import { describeError, rateLimitMessage } from '../lib/errorMessages';

// Counts down from "start" seconds; its timer is cleared when the component unmounts
function useCountdown(start: number | null): number | null {
  const [endsAt] = useState(() => (start === null ? null : Date.now() + start * 1000));
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    if (endsAt === null) return;
    const id = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(id);
  }, [endsAt]);
  return endsAt === null ? null : Math.max(0, Math.ceil((endsAt - now) / 1000));
}

// Reference the member can select and copy and read out to support
export function Reference({ id }: { id: string | null }) {
  if (!id) {
    return <p style={{ fontSize: 12 }}>No reference available (the request never reached RondiTrack).</p>;
  }
  return (
    <p style={{ fontSize: 12 }}>
      Reference: <code style={{ fontFamily: 'monospace', userSelect: 'all' }}>{id}</code>
    </p>
  );
}

type Props = { error: unknown; onRetry?: () => void; onBack?: () => void };

// The ONE error view every screen uses
export function ErrorView({ error, onRetry, onBack }: Props) {
  const d = describeError(error);
  const seconds = useCountdown(d.retryAfterSeconds);

  // Session ended: App shows the sign-in form, so we show nothing here (no stacked error)
  if (d.kind === 'sessionEnded') return null;

  const message =
    d.kind === 'rateLimited' && seconds !== null
      ? seconds > 0
        ? rateLimitMessage(seconds)
        : 'You can try again now.'
      : d.message;

  return (
    <div role="alert" style={{ border: '1px solid #c33', padding: 16, borderRadius: 8, margin: '16px 0' }}>
      <h2 style={{ marginTop: 0 }}>{d.title}</h2>
      <p>{message}</p>
      {d.kind === 'notFound' && onBack && <button onClick={onBack}>Back to my stokvels</button>}
      {d.kind !== 'notFound' && onRetry && (
        // for a 429 the button stays disabled until the countdown reaches zero
        <button onClick={onRetry} disabled={seconds !== null && seconds > 0}>
          Try again
        </button>
      )}
      <Reference id={d.correlationId} />
    </div>
  );
}