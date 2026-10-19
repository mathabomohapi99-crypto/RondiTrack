// NEW: turns an integer number of cents into "R 150,50" without ever using fractional floats.
const zarFormatter = new Intl.NumberFormat('en-ZA', {
  style: 'currency',
  currency: 'ZAR',
})

export function formatCents(cents: number): string {
  // Reject 10.5, NaN, Infinity, or numbers too big to be exact
  if (!Number.isSafeInteger(cents)) {
    throw new Error('not a whole number of cents')
  }

  // BigInt keeps everything as whole numbers, so there is no float rounding at all
  const big = BigInt(cents)
  const negative = big < 0n
  const abs = negative ? -big : big

  // Integer division and remainder: 15050 cents -> 150 rands and 50 cents
  const rands = abs / 100n
  const rest = abs % 100n

  // Build an exact decimal string like "150.50" (pad so 5 cents becomes "05")
  const decimal = `${negative ? '-' : ''}${rands}.${rest.toString().padStart(2, '0')}`

  // Intl accepts an exact decimal string, so it formats without turning it into a float
  return zarFormatter.format(decimal as `${number}`)
}