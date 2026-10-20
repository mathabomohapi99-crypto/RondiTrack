// NEW: South African due-date maths. Pure functions: "now" is always passed in, never read here.
const MS_PER_DAY = 86_400_000
// South Africa is UTC+2 all year with no daylight saving since 1944, so a fixed offset is safe.
const SA_OFFSET_MS = 2 * 3_600_000
const CALENDAR_DATE = /^\d{4}-\d{2}-\d{2}$/

// Which South African calendar day an instant falls on (a whole number of days since 1970).
export function saDayIndex(instant: Date): number {
  return Math.floor((instant.getTime() + SA_OFFSET_MS) / MS_PER_DAY)
}

// The South African calendar day a due date means. "2026-10-31" is that day itself;
// an instant like "2026-10-31T21:59:59Z" is the SA day it falls on.
function dueDayIndex(due: string): number {
  if (CALENDAR_DATE.test(due)) {
    const year = Number(due.slice(0, 4))
    const month = Number(due.slice(5, 7))
    const day = Number(due.slice(8, 10))
    return Date.UTC(year, month - 1, day) / MS_PER_DAY
  }
  const instant = new Date(due)
  if (Number.isNaN(instant.getTime())) throw new Error(`not a valid due date: ${due}`)
  return saDayIndex(instant)
}

// The moment the due day ENDS in Johannesburg. "2026-10-31" -> 2026-10-31T22:00:00Z (not 00:00Z).
export function dueDateToDeadline(due: string): Date {
  return new Date((dueDayIndex(due) + 1) * MS_PER_DAY - SA_OFFSET_MS)
}

// Milliseconds until the next Johannesburg midnight. Exactly at midnight it returns 24 hours, never 0.
export function msUntilNextSaMidnight(now: Date): number {
  return (saDayIndex(now) + 1) * MS_PER_DAY - SA_OFFSET_MS - now.getTime()
}

function days(n: number): string {
  return n === 1 ? 'day' : 'days'
}

// The countdown words, or null when no countdown should show (cycle Closed or PaidOut).
export function dueCountdown(due: string, now: Date, cycleStatus: string): string | null {
  if (cycleStatus !== 'Open') return null

  if (now.getTime() >= dueDateToDeadline(due).getTime()) {
    const late = saDayIndex(now) - dueDayIndex(due)
    return `Overdue by ${late} ${days(late)}`
  }

  const left = dueDayIndex(due) - saDayIndex(now)
  if (left === 0) return 'Due today'
  if (left === 1) return 'Due tomorrow'
  return `Due in ${left} days`
}