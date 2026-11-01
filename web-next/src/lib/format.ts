// All dates are shown in South African time, whatever time zone the server or laptop is in.
const TIME_ZONE = 'Africa/Johannesburg';

const dateFormatter = new Intl.DateTimeFormat('en-ZA', { dateStyle: 'medium', timeZone: TIME_ZONE });
const dateTimeFormatter = new Intl.DateTimeFormat('en-ZA', {
  dateStyle: 'medium',
  timeStyle: 'short',
  timeZone: TIME_ZONE,
});

// Turns rands into text, e.g. 150 -> "R 150,00".
export function formatMoney(amount: number): string {
  return new Intl.NumberFormat('en-ZA', { style: 'currency', currency: 'ZAR' }).format(amount);
}

// NEW 8.2: integer cents -> "R 150,50"
export function formatCents(cents: number): string {
  return formatMoney(cents / 100);
}

// An ISO instant -> a short date in South African time
export function formatDate(iso: string | null | undefined): string {
  if (!iso) return 'Not set';
  return dateFormatter.format(new Date(iso));
}

// NEW 8.3: an ISO instant -> date and time in South African time (23:30 on 31 Oct stays 31 Oct)
export function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return 'Not set';
  return dateTimeFormatter.format(new Date(iso));
}