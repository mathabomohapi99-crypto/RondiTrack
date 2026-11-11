import { describe, expect, it } from 'vitest';
import { formatCents } from './format';
import { centsToInput, MAX_CENTS, parseRandsToCents } from './rands';

describe('parseRandsToCents: the ways South Africans write amounts', () => {
  it.each([
    ['1500', 150_000],
    ['1 500', 150_000],
    ['1 500,50', 150_050],
    ['R1500', 150_000],
    ['R 1 500,50', 150_050],
    ['1\u00a0500,50', 150_050], // the no-break space Intl itself produces
    ['1 500,5', 150_050], // one decimal place is 50 cents, not 5
    ['150,5', 15_050],
    [',5', 50],
    ['0', 0],
    ['0,01', 1],
    ['  R 150  ', 15_000],
    ['1000000', MAX_CENTS],
  ])('%j gives %i cents', (input, cents) => {
    expect(parseRandsToCents(input)).toBe(cents);
  });

  // The classic: these all go wrong if the code multiplies a decimal by 100
  it.each([
    ['19,99', 1999],
    ['19.99', 1999],
    ['0.29', 29], // 0.29 * 100 = 28.999999999999996
    ['4.35', 435], // 4.35 * 100 = 434.99999999999994
    ['1.15', 115],
    ['8.2', 820],
  ])('%j is exactly %i cents (never multiplies a decimal)', (input, cents) => {
    expect(parseRandsToCents(input)).toBe(cents);
  });
});

describe('parseRandsToCents: refuses anything it would have to guess at', () => {
  it.each([
    ['150,505', 'three decimal places'],
    ['150.505', 'three decimal places'],
    ['1,500.50', 'is the comma thousands or decimals?'],
    ['1.500,50', 'is the dot thousands or decimals?'],
    ['abc', 'letters'],
    ['R abc', 'letters'],
    ['12abc', 'letters after digits'],
    ['', 'empty'],
    ['   ', 'only spaces'],
    ['R', 'only the symbol'],
    ['-5', 'negative'],
    ['-0,50', 'negative'],
    ['1e3', 'exponent'],
    ['1,5,5', 'two commas'],
    ['1000000,01', 'above the maximum by one cent'],
    ['1000001', 'above the maximum'],
    ['99999999999999999999', 'far too big'],
  ])('%j gives null (%s)', (input) => {
    expect(parseRandsToCents(input)).toBeNull();
  });
});

describe('formatting round-trips', () => {
  it.each([0, 1, 99, 100, 150_050, MAX_CENTS])('%i cents formats and parses back to the same cents', (cents) => {
    expect(parseRandsToCents(formatCents(cents))).toBe(cents);
  });

  it('centsToInput and the parser agree for every amount up to R 1 000', () => {
    for (let cents = 0; cents <= 100_000; cents++) {
      expect(parseRandsToCents(centsToInput(cents))).toBe(cents);
    }
  });

  it('centsToInput pads the cents', () => {
    expect(centsToInput(15_050)).toBe('150.50');
    expect(centsToInput(5)).toBe('0.05');
  });
});