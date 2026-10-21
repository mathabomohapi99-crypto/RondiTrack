import { describe, it, expect } from 'vitest';
import { ApiError } from '../api/apiError';
import { describeError, parseRetryAfter } from './errorMessages';

const NOW = Date.parse('2026-10-21T10:00:00Z');

describe('parseRetryAfter', () => {
  it('reads seconds', () => expect(parseRetryAfter('23', NOW)).toBe(23));
  it('reads an HTTP date', () =>
    expect(parseRetryAfter('Wed, 21 Oct 2026 10:00:30 GMT', NOW)).toBe(30));
  it('gives 0 for a date in the past', () =>
    expect(parseRetryAfter('Wed, 21 Oct 2026 09:00:00 GMT', NOW)).toBe(0));
  it('gives null for garbage', () => {
    expect(parseRetryAfter('soon', NOW)).toBeNull();
    expect(parseRetryAfter(null, NOW)).toBeNull();
    expect(parseRetryAfter('', NOW)).toBeNull();
  });
});

describe('describeError', () => {
  it('404 says stokvel not found and keeps the reference', () => {
    const d = describeError(new ApiError(404, { correlationId: 'abc' }, null));
    expect(d.kind).toBe('notFound');
    expect(d.title).toBe('Stokvel not found');
    expect(d.correlationId).toBe('abc');
  });
  it('429 shows the number from the header', () => {
    const d = describeError(new ApiError(429, null, '23'));
    expect(d.message).toBe('Too many requests. Try again in 23 seconds.');
  });
  it('wrong password never says which part was wrong', () => {
    const d = describeError(new ApiError(401, null, null), 'login');
    expect(d.message).toBe('Email or password is incorrect.');
  });
  it('network failure has no reference', () => {
    const d = describeError(new TypeError('Failed to fetch'));
    expect(d.kind).toBe('network');
    expect(d.correlationId).toBeNull();
  });
  it('500 never shows the detail', () => {
    const d = describeError(new ApiError(500, { detail: 'SELECT * FROM secrets', correlationId: 'x1' }, null));
    expect(d.message).not.toContain('SELECT');
    expect(d.correlationId).toBe('x1');
  });
  it('400 returns field messages', () => {
    const d = describeError(new ApiError(400, { errors: { Email: ['Required'] } }, null));
    expect(d.fieldErrors.email).toEqual(['Required']);
  });
});