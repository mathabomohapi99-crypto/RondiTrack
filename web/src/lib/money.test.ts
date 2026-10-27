// NEW: tests for formatCents, including the no-break-space (NBSP) detail.
import { describe, expect, it } from 'vitest'
import { formatCents } from './money'

// The expected strings use U+00A0, not a normal space, so we spell it out.
const NBSP = '\u00A0'

describe('formatCents', () => {
  it('formats 10 cents', () => {
    expect(formatCents(10)).toBe(`R${NBSP}0,10`)
  })

  it('formats zero', () => {
    expect(formatCents(0)).toBe(`R${NBSP}0,00`)
  })

  it('formats 15050 cents', () => {
    expect(formatCents(15050)).toBe(`R${NBSP}150,50`)
  })

  it('formats thousands with no-break-space separators', () => {
    expect(formatCents(1234567)).toBe(`R${NBSP}12${NBSP}345,67`)
  })

  it('throws when the input is not a whole number of cents', () => {
    expect(() => formatCents(10.5)).toThrow('not a whole number of cents')
  })

  // My own edge case: a large amount a stokvel could hold (R 999 999,99)
  it('formats a large amount exactly', () => {
    expect(formatCents(99999999)).toBe(`R${NBSP}999${NBSP}999,99`)
  })
})