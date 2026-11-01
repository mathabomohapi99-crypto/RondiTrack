// The 7.4 cents parser, so "150.505" is refused instead of rounded. Works on text, never floats.
export function parseRandsToCents(text: string): number | null {
  let t = text.trim().replace(/^R\s*/i, '').replace(/\s/g, '');
  if (t.includes(',') && t.includes('.')) return null; // "1,500.00" is ambiguous: refuse it
  t = t.replace(',', '.'); // South African decimal comma
  if (!/^\d+(\.\d{1,2})?$/.test(t)) return null; // at most 2 decimals
  const [rands, cents = ''] = t.split('.');
  const total = Number(rands) * 100 + Number(cents.padEnd(2, '0'));
  return Number.isSafeInteger(total) ? total : null;
}

// 15050 -> "150.50" (to prefill the amount box)
export function centsToInput(cents: number): string {
  return `${Math.floor(cents / 100)}.${String(cents % 100).padStart(2, '0')}`;
}