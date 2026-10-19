// NEW: tests proving the midnight case and the calendar-date case.
import { describe, expect, it } from 'vitest'
import { formatDate, formatDateTime } from './dates'

describe('dates', () => {
  // The midnight case: 22:30 UTC on 31 Oct is already 00:30 on 1 Nov in Johannesburg
  it('shows the midnight payment on 01 Nov 2026, 00:30', () => {
    expect(formatDateTime('2026-10-31T22:30:00Z')).toBe('01 Nov 2026, 00:30')
  })

  it('shows the date part of that instant as 01 Nov 2026', () => {
    expect(formatDate('2026-10-31T22:30:00Z')).toBe('01 Nov 2026')
  })

  it('keeps a South African calendar date on the same day', () => {
    expect(formatDate('2026-10-31')).toBe('31 Oct 2026')
  })

  it('throws on a value that is not a date', () => {
    expect(() => formatDate('banana')).toThrow()
  })
})