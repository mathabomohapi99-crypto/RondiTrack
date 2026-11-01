// Builds the API path for a history page. THIS is where the South African days become UTC instants
// (through the one converter). The page and the Route Handler both call it.
import { southAfricanDaysToUtcRange } from './saDates';
import type { ContributionFilters } from './contributionSearchParams';

export function contributionsPath(stokvelId: string, f: ContributionFilters, pageSize: number, pageToken = '') {
  const qs = new URLSearchParams();
  qs.set('pageSize', String(pageSize));
  qs.set('sort', f.sort);
  if (f.member) qs.set('member', f.member);

  let rangeIgnored = false;
  try {
    const range = southAfricanDaysToUtcRange(f.from || null, f.to || null);
    if (range.from) qs.set('from', range.from);
    if (range.to) qs.set('to', range.to);
  } catch {
    rangeIgnored = true; // "to" is before "from": both dates are ignored and the page says so
  }

  if (pageToken) qs.set('pageToken', pageToken);
  return { path: `/stokvels/${stokvelId}/contributions?${qs.toString()}`, rangeIgnored };
}