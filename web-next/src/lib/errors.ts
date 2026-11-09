import { messageFor, withReference } from './errorMessages';

// The one error type for "anything else" from the API. Its message is already friendly and has the reference.
export class ApiError extends Error {
  status: number;
  correlationId?: string;

  constructor(status: number, correlationId?: string) {
    super(withReference(messageFor(status), correlationId));
    this.name = 'ApiError';
    this.status = status;
    this.correlationId = correlationId;
  }
}

// Finds the correlationId in a problem+json body (the API puts it there).
export function correlationIdFrom(body: unknown): string | undefined {
  if (body && typeof body === 'object' && 'correlationId' in body) {
    const id = (body as Record<string, unknown>).correlationId;
    if (typeof id === 'string') return id;
  }
  return undefined;
}

// NEW 8.2: the "detail" sentence of a problem+json body (used for 409 and 422, where it explains why).
export function detailFrom(body: unknown): string | undefined {
  if (body && typeof body === 'object' && 'detail' in body) {
    const d = (body as Record<string, unknown>).detail;
    if (typeof d === 'string' && d) return d;
  }
  return undefined;
}

// NEW 8.2: the "errors" map of a 400, as { lowercasedField: firstMessage }.
export function fieldErrorsFrom(body: unknown): Record<string, string> {
  const out: Record<string, string> = {};
  if (body && typeof body === 'object' && 'errors' in body) {
    const raw = (body as Record<string, unknown>).errors;
    if (raw && typeof raw === 'object') {
      for (const [key, value] of Object.entries(raw as Record<string, unknown>)) {
        if (Array.isArray(value) && value.length > 0) out[key.toLowerCase()] = String(value[0]);

// NEW 9.1: the short "code" of a problem+json body (e.g. invite_expired, stale_preview, proof_locked)
export function codeFrom(body: unknown): string | undefined {
  if (body && typeof body === 'object' && 'code' in body) {
    const c = (body as Record<string, unknown>).code;
    if (typeof c === 'string') return c;
  }
  return undefined;
}
      }
    }
  }
  return out;
}