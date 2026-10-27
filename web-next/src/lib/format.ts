// Turns rands into text, e.g. 150 -> "R 150,00". (The API sends rands, so this replaces formatCents.)
export function formatMoney(amount: number): string {
  return new Intl.NumberFormat('en-ZA', { style: 'currency', currency: 'ZAR' }).format(amount);
}

// Turns an ISO date into a short readable date.
export function formatDate(iso: string | null | undefined): string {
  if (!iso) return 'Not set';
  return new Date(iso).toLocaleDateString('en-ZA', { year: 'numeric', month: 'short', day: 'numeric' });
}