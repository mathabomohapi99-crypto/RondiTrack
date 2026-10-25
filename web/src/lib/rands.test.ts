import { describe, expect, it } from 'vitest';
import { centsToInput, parseRandsToCents } from './rands';

describe('parseRandsToCents', () => {
  it('parses normal amounts exactly', () => {
    expect(parseRandsToCents('150.50')).toBe(15050);
    expect(parseRandsToCents('150.5')).toBe(15050);
    expect(parseRandsToCents('19.99')).toBe(1999);
    expect(parseRandsToCents('150')).toBe(15000);
  });

  it('accepts a space for thousands and a comma for decimals', () => {
    expect(parseRandsToCents('1 500,50')).toBe(150050);
    expect(parseRandsToCents('1\u00a0500,50')).toBe(150050);
  });

  it('refuses anything that would need rounding or is not money', () => {
    expect(parseRandsToCents('150.505')).toBeNull();
    expect(parseRandsToCents('1,500.50')).toBeNull();
    expect(parseRandsToCents('abc')).toBeNull();
    expect(parseRandsToCents('')).toBeNull();
    expect(parseRandsToCents('-5')).toBeNull();
  });
});

describe('round trip', () => {
  // every whole number of cents from 0 to 100 000 must survive text and back
  it('parse(centsToInput(c)) === c for 0..100000', () => {
    for (let cents = 0; cents <= 100_000; cents++) {
      expect(parseRandsToCents(centsToInput(cents))).toBe(cents);
    }
  });
});