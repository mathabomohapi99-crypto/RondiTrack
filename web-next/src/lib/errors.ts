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