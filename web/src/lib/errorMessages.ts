import { ApiError } from '../api/apiError';

export type ErrorKind =
  | 'network'
  | 'rateLimited'
  | 'badCredentials'
  | 'sessionEnded'
  | 'notFound'
  | 'validation'
  | 'generic';

export type DescribedError = {
  kind: ErrorKind;
  title: string;
  message: string;
  correlationId: string | null; // null = there is no reference to show
  retryAfterSeconds: number | null;
  fieldErrors: Record<string, string[]>; // keys are lower-case, e.g. "email"
};

// Retry-After is either seconds ("23") or an HTTP date. Returns seconds or null.
export function parseRetryAfter(header: string | null | undefined, now: number = Date.now()): number | null {
  if (!header) return null;
  const text = header.trim();
  if (/^\d+$/.test(text)) return Number(text);
  const when = Date.parse(text);
  if (Number.isNaN(when)) return null;
  return Math.max(0, Math.ceil((when - now) / 1000)); // date in the past = 0
}

export function rateLimitMessage(seconds: number | null): string {
  if (seconds === null) return 'Too many requests. Please wait a moment and try again.';
  return `Too many requests. Try again in ${seconds} ${seconds === 1 ? 'second' : 'seconds'}.`;
}

const asString = (v: unknown): string | null => (typeof v === 'string' && v ? v : null);

// Turns ANY error into plain words. Never uses the "detail" field (it can leak internals on a 500).
export function describeError(error: unknown, context: 'data' | 'login' = 'data'): DescribedError {
  const empty = { correlationId: null, retryAfterSeconds: null, fieldErrors: {} };

  // fetch itself failed (API down, no internet): there is no response and no reference
  if (error instanceof TypeError) {
    return {
      ...empty,
      kind: 'network',
      title: "Can't reach RondiTrack",
      message: 'Check your connection.',
    };
  }

  if (!(error instanceof ApiError)) {
    return {
      ...empty,
      kind: 'generic',
      title: 'Something went wrong',
      message: 'Something went wrong on our side. Please try again.',
    };
  }

  const correlationId = asString(error.problem?.correlationId);
  const base = { ...empty, correlationId };

  if (error.status === 429) {
    const seconds = parseRetryAfter(error.retryAfter);
    return {
      ...base,
      kind: 'rateLimited',
      title: 'Too many requests',
      message: rateLimitMessage(seconds),
      retryAfterSeconds: seconds,
    };
  }
  if (error.status === 401) {
    return context === 'login'
      ? { ...base, kind: 'badCredentials', title: 'Sign in failed', message: 'Email or password is incorrect.' }
      : { ...base, kind: 'sessionEnded', title: 'Session ended', message: 'Please sign in again.' };
  }
  if (error.status === 404) {
    return {
      ...base,
      kind: 'notFound',
      title: 'Stokvel not found',
      message: "This stokvel doesn't exist, or you are not a member of it.",
    };
  }
  if (error.status === 400) {
    const fieldErrors: Record<string, string[]> = {};
    const raw = error.problem?.errors;
    if (raw && typeof raw === 'object') {
      for (const [key, value] of Object.entries(raw)) {
        if (Array.isArray(value)) fieldErrors[key.toLowerCase()] = value.map(String);
      }
    }
    return { ...base, kind: 'validation', title: 'Please check your details', message: 'Some fields need your attention.', fieldErrors };
  }
  return {
    ...base,
    kind: 'generic',
    title: 'Something went wrong',
    message: 'Something went wrong on our side. Please try again in a moment.',
  };
}