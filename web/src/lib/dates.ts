// NEW: formats dates in South African time, whatever time zone the laptop is set to.
const TIME_ZONE = 'Africa/Johannesburg'

// "01 Nov 2026, 00:30"
const dateTimeFormatter = new Intl.DateTimeFormat('en-ZA', {
  dateStyle: 'medium',
  timeStyle: 'short',
  timeZone: TIME_ZONE,
})

// "01 Nov 2026"
const dateFormatter = new Intl.DateTimeFormat('en-ZA', {
  dateStyle: 'medium',
  timeZone: TIME_ZONE,
})

// Matches a plain calendar date like "2026-10-31" (no time, no Z)
const CALENDAR_DATE = /^\d{4}-\d{2}-\d{2}$/

function toDate(value: string): Date {
  // A plain calendar date is already a South African day. Noon UTC (14:00 in SA)
  // means no time zone can push it onto the wrong day.
  const date = CALENDAR_DATE.test(value) ? new Date(`${value}T12:00:00Z`) : new Date(value)
  if (Number.isNaN(date.getTime())) {
    throw new Error(`not a valid date: ${value}`)
  }
  return date
}

// For UTC instants like "2026-10-31T22:30:00Z" (date AND time, in SA time)
export function formatDateTime(value: string): string {
  return dateTimeFormatter.format(toDate(value))
}

// Date only. Works with both a UTC instant and a calendar date
export function formatDate(value: string): string {
  return dateFormatter.format(toDate(value))
}