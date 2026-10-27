'use client'; // Error pages must be Client Components, and Try again needs an onClick.

type Props = {
  error: Error & { digest?: string };
  retry?: () => void; // the retry function; some Next 16 versions name it differently, so all names are accepted
  unstable_retry?: () => void;
  reset?: () => void;
};

// Shows the friendly message and its reference. Never a stack trace.
export default function AppError({ error, retry, unstable_retry, reset }: Props) {
  const tryAgain = retry ?? unstable_retry ?? reset;

  return (
    <div className="rounded border border-red-300 p-6">
      <h1 className="text-xl font-bold">Something went wrong</h1>
      <p className="mt-2 text-sm">{error.message}</p>
      {error.digest && <p className="mt-1 text-xs text-gray-500">Reference: {error.digest}</p>}
      <button type="button" onClick={() => tryAgain?.()} className="mt-4 rounded border px-3 py-1">
        Try again
      </button>
    </div>
  );
}