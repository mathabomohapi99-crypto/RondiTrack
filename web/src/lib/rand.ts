// Shows whole cents (like 11111) as South African rand ("R 111,11")
const zar = new Intl.NumberFormat('en-ZA', { style: 'currency', currency: 'ZAR' });

export function formatCentsAsRand(cents: number): string {
  return zar.format(cents / 100); // divide by 100 only when displaying
}