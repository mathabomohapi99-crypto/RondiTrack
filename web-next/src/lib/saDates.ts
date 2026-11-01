// The ONLY place that turns South African calendar days into UTC instants.
// South Africa is UTC+2 all year (no daylight saving), so 00:00 SAST = 22:00 UTC the day before.
// The range is half-open: [from, to). "to" is 00:00 SAST on the day AFTER the last day. No 23:59:59 anywhere.
const SAST_OFFSET_MS = 2 * 60 * 60 * 1000;
const DAY = /^(\d{4})-(\d{2})-(\d{2})$/;

export type UtcRange = { from?: string; to?: string };

// 00:00 SAST of a YYYY-MM-DD day (plus optional extra days), as milliseconds. Impossible dates are rejected.
function midnightSastMs(day: string, daysToAdd = 0): number {
  const match = DAY.exec(day);
  if (!match) throw new RangeError(`Not a YYYY-MM-DD date: ${day}`);
  const year = Number(match[1]);
  const month = Number(match[2]);
  const date = Number(match[3]);
  const check = new Date(Date.UTC(year, month - 1, date));
  // 2026-02-30 would roll over to 2 March, so we compare and refuse instead
  if (check.getUTCFullYear() !== year || check.getUTCMonth() !== month - 1 || check.getUTCDate() !== date) {
    throw new RangeError(`Not a real calendar date: ${day}`);
  }
  return Date.UTC(year, month - 1, date + daysToAdd) - SAST_OFFSET_MS;
}

export function isRealCalendarDate(day: string): boolean {
  try {
    midnightSastMs(day);
    return true;
  } catch {
    return false;
  }
}

const toIso = (ms: number) => new Date(ms).toISOString().replace('.000Z', 'Z');

// from-only and to-only are fine. A "to" before "from" throws (the page then ignores both dates).
export function southAfricanDaysToUtcRange(from?: string | null, to?: string | null): UtcRange {
  const startMs = from ? midnightSastMs(from) : undefined;
  const endMs = to ? midnightSastMs(to, 1) : undefined;
  if (startMs !== undefined && endMs !== undefined && endMs <= startMs) {
    throw new RangeError('The end date is before the start date');
  }
  const range: UtcRange = {};
  if (startMs !== undefined) range.from = toIso(startMs);
  if (endMs !== undefined) range.to = toIso(endMs);
  return range;
}