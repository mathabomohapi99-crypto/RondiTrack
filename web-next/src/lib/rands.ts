// Whole cents only. Works on TEXT with integer maths: a decimal is never multiplied (19.99 * 100 = 1998.9999999999998).
export const MAX_CENTS = 100_000_000; // R 1 000 000,00 = Stokvel.MaxContribution in the API

// "R 1 500,50", "1 500,5", ",5" -> cents. Anything it would have to GUESS at -> null (never a rounded number).
export function parseRandsToCents(text: string): number | null {
  let t = text.trim().replace(/^R\s*/i, '').replace(/\s/g, ''); // \s also removes the no-break space Intl produces
  if (t.includes(',') && t.includes('.')) return null; // "1,500.50": thousands or decimals? refuse
  t = t.replace(',', '.'); // South African decimal comma (a second comma stays and fails below)
  if (t.startsWith('.')) t = `0${t}`; // ",5" is 50 cents
  if (!/^\d+(\.\d{1,2})?$/.test(t)) return null; // at most 2 decimals, digits only, no sign
  const [rands, cents = ''] = t.split('.');
  if (rands.length > 12) return null; // far beyond the maximum, and keeps the sum exact
  const total = Number(rands) * 100 + Number(cents.padEnd(2, '0')); // integer * 100: exact
  return total <= MAX_CENTS ? total : null;
}

// 15050 -> "150.50" (to prefill the amount box)
export function centsToInput(cents: number): string {
  return `${Math.floor(cents / 100)}.${String(cents % 100).padStart(2, '0')}`;
}