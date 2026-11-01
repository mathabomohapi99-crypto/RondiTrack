import { describe, expect, it } from 'vitest';
import { centsToInput, parseRandsToCents } from './rands';

describe('parseRandsToCents', () => {
  it('turns text into integer cents', () => {
    expect(parseRandsToCents('150')).toBe(15000);
    expect(parseRandsToCents('150.5')).toBe(15050);
    expect(parseRandsToCents('150,50')).toBe(15050);
    expect(parseRandsToCents('R 150.50')).toBe(15050);
  });

  it('refuses more than 2 decimals, junk and ambiguous values', () => {
    expect(parseRandsToCents('150.505')).toBeNull();
    expect(parseRandsToCents('abc')).toBeNull();
    expect(parseRandsToCents('')).toBeNull();
    expect(parseRandsToCents('-5')).toBeNull();
    expect(parseRandsToCents('1,500.00')).toBeNull();
  });

  it('centsToInput pads the cents', () => {
    expect(centsToInput(15050)).toBe('150.50');
    expect(centsToInput(5)).toBe('0.05');
  });
});