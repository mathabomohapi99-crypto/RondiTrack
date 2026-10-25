// NEW 7.4: money <-> text for the form. Integer maths on text only: no parseFloat, no multiplying decimals,
// no toFixed, no rounding.

// DECISION: "1 500,50" IS accepted (spaces or no-break spaces for thousands, a comma for decimals).
// "1,500.50" is refused (ambiguous), and anything with more than 2 decimals is refused (it would need rounding).
export function parseRandsToCents(input: string): number | null {
  // remove spaces / no-break spaces, then turn a decimal comma into a dot
  const cleaned = input.trim().replace(/[\s\u00a0]/g, '').replace(',', '.');

  // digits, then optionally a dot with 1 or 2 digits
  const match = /^(\d+)(?:\.(\d{1,2}))?$/.exec(cleaned);
  if (!match) return null;

  const whole = match[1];
  const fraction = (match[2] ?? '').padEnd(2, '0'); // "5" -> "50", missing -> "00"

  // glue the digits as TEXT ("150" + "50" = "15050"), so no float maths ever happens
  const cents = parseInt(whole + fraction, 10);
  return Number.isSafeInteger(cents) ? cents : null;
}

// The reverse, for prefilling the input: 15050 -> "150.50", 15000 -> "150", 5 -> "0.05"
export function centsToInput(cents: number): string {
  if (!Number.isSafeInteger(cents) || cents < 0) {
    throw new RangeError('cents must be a non-negative whole number');
  }
  const digits = String(cents).padStart(3, '0'); // always at least 3 digits so we can split off 2 decimals
  const whole = digits.slice(0, -2);
  const fraction = digits.slice(-2);
  return fraction === '00' ? whole : `${whole}.${fraction}`;
}