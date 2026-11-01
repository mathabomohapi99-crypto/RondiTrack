import { describe, expect, it } from 'vitest';
import { needsRefresh, refreshMarginMs } from './refreshPolicy';

describe('refreshMarginMs', () => {
  it('defaults to 120 seconds when unset or nonsense', () => {
    expect(refreshMarginMs(undefined)).toBe(120_000);
    expect(refreshMarginMs('')).toBe(120_000);
    expect(refreshMarginMs('banana')).toBe(120_000);
    expect(refreshMarginMs('-5')).toBe(120_000);
  });
  it('uses a valid setting', () => {
    expect(refreshMarginMs('30')).toBe(30_000);
  });
});

describe('needsRefresh', () => {
  it('is false with plenty of time and true inside the margin or after the end', () => {
    const now = 1_000_000;
    expect(needsRefresh(now + 600_000, now, 120_000)).toBe(false);
    expect(needsRefresh(now + 120_000, now, 120_000)).toBe(true);
    expect(needsRefresh(now + 10_000, now, 120_000)).toBe(true);
    expect(needsRefresh(now - 1, now, 120_000)).toBe(true);
  });
});