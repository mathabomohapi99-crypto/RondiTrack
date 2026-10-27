// NEW: boundary tests for the due-date maths. Every test passes its own "now".
import { describe, expect, it } from 'vitest'
import { dueCountdown, dueDateToDeadline, msUntilNextSaMidnight } from './countdown'

const at = (iso: string) => new Date(iso)

describe('dueDateToDeadline', () => {
  it('turns a calendar date into the END of that day in Johannesburg', () => {
    expect(dueDateToDeadline('2026-10-31').toISOString()).toBe('2026-10-31T22:00:00.000Z')
  })

  it('does not treat the date as UTC midnight', () => {
    expect(dueDateToDeadline('2026-10-31').toISOString()).not.toBe('2026-10-31T00:00:00.000Z')
  })

  it('uses the South African day of an instant', () => {
    expect(dueDateToDeadline('2026-10-31T21:59:59Z').toISOString()).toBe('2026-10-31T22:00:00.000Z')
  })

  it('throws on a value that is not a date', () => {
    expect(() => dueDateToDeadline('banana')).toThrow()
  })
})

describe('dueCountdown', () => {
  it('23:30 SAST on the due date is Due today, not overdue', () => {
    expect(dueCountdown('2026-10-31', at('2026-10-31T21:30:00Z'), 'Open')).toBe('Due today')
  })

  it('00:30 SAST on the due date is Due today', () => {
    expect(dueCountdown('2026-10-31', at('2026-10-30T22:30:00Z'), 'Open')).toBe('Due today')
  })

  it('one second after the deadline is Overdue by 1 day', () => {
    expect(dueCountdown('2026-10-31', at('2026-10-31T22:00:01Z'), 'Open')).toBe('Overdue by 1 day')
  })

  it('counts several days overdue', () => {
    expect(dueCountdown('2026-10-31', at('2026-11-03T10:00:00Z'), 'Open')).toBe('Overdue by 3 days')
  })

  it('the day before is Due tomorrow', () => {
    expect(dueCountdown('2026-10-31', at('2026-10-30T10:00:00Z'), 'Open')).toBe('Due tomorrow')
  })

  it('later shows Due in N days', () => {
    expect(dueCountdown('2026-10-31', at('2026-10-26T10:00:00Z'), 'Open')).toBe('Due in 5 days')
  })

  it('shows no countdown for Closed or PaidOut cycles', () => {
    expect(dueCountdown('2026-10-31', at('2026-10-31T10:00:00Z'), 'Closed')).toBeNull()
    expect(dueCountdown('2026-10-31', at('2026-10-31T10:00:00Z'), 'PaidOut')).toBeNull()
  })
})

describe('msUntilNextSaMidnight', () => {
  it('from 23:30 SAST it is 30 minutes', () => {
    expect(msUntilNextSaMidnight(at('2026-10-31T21:30:00Z'))).toBe(30 * 60 * 1000)
  })

  it('from exactly midnight it is 24 hours, not 0', () => {
    expect(msUntilNextSaMidnight(at('2026-10-31T22:00:00Z'))).toBe(24 * 60 * 60 * 1000)
  })
})