// When must proxy.ts refresh? Pure functions, so they are easy to test.

// 10-minute access token, 2-minute margin: a page render and its API calls fit inside the margin,
// but we do not refresh on almost every click either.
export const DEFAULT_REFRESH_MARGIN_SECONDS = 120;

// Reads REFRESH_MARGIN_SECONDS (only for local testing of the race). Bad values use the default.
export function refreshMarginMs(raw: string | undefined = process.env.REFRESH_MARGIN_SECONDS): number {
  const n = Number(raw);
  if (raw === undefined || raw.trim() === '' || !Number.isFinite(n) || n < 0) {
    return DEFAULT_REFRESH_MARGIN_SECONDS * 1000;
  }
  return n * 1000;
}

// True when the access token has less than the margin left (or is already over)
export function needsRefresh(accessExpiresAt: number, now: number, marginMs: number): boolean {
  return accessExpiresAt - now <= marginMs;
}