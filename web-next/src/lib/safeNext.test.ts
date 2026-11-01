import { describe, expect, it } from 'vitest';
import { DEFAULT_AFTER_LOGIN, safeNextPath } from './safeNext';

describe('safeNextPath', () => {
  it('follows a path on our own site, with its query', () => {
    expect(safeNextPath('/stokvels/abc')).toBe('/stokvels/abc');
    expect(safeNextPath('/stokvels/abc/contributions?member=1&from=2026-10-01')).toBe(
      '/stokvels/abc/contributions?member=1&from=2026-10-01',
    );
  });

  it('refuses //evil.example and /\\evil.example', () => {
    expect(safeNextPath('//evil.example')).toBe(DEFAULT_AFTER_LOGIN);
    expect(safeNextPath('/\\evil.example')).toBe(DEFAULT_AFTER_LOGIN);
  });

  it('refuses full URLs, other schemes, empty and missing values', () => {
    expect(safeNextPath('https://evil.example')).toBe(DEFAULT_AFTER_LOGIN);
    expect(safeNextPath('javascript:alert(1)')).toBe(DEFAULT_AFTER_LOGIN);
    expect(safeNextPath('')).toBe(DEFAULT_AFTER_LOGIN);
    expect(safeNextPath(null)).toBe(DEFAULT_AFTER_LOGIN);
    expect(safeNextPath(undefined)).toBe(DEFAULT_AFTER_LOGIN);
  });

  it('refuses control characters', () => {
    expect(safeNextPath('/\t/evil.example')).toBe(DEFAULT_AFTER_LOGIN);
  });
});