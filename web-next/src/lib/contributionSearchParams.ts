// ONE parsers file, imported from nuqs/server, shared by the page, the Route Handler and the filter controls.
// Every parser is an allow-list or a strict format. Anything else falls back to the default (no error page).
import { createLoader, createParser, parseAsStringLiteral } from 'nuqs/server';
import { isRealCalendarDate } from './saDates';

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export const SORTS = ['-date', 'date', 'amount', '-amount'] as const;

// member: a real UUID, or it is ignored
const parseAsMemberId = createParser<string>({
  parse: (value) => (UUID.test(value) ? value.toLowerCase() : null),
  serialize: (value) => value,
});

// from / to: a real YYYY-MM-DD day (2026-02-30 and "yesterday" are ignored). Kept as TEXT, never as a Date.
const parseAsDay = createParser<string>({
  parse: (value) => (isRealCalendarDate(value) ? value : null),
  serialize: (value) => value,
});

export const contributionParsers = {
  member: parseAsMemberId.withDefault(''), // '' = everyone
  from: parseAsDay.withDefault(''), // '' = no start date
  to: parseAsDay.withDefault(''), // '' = no end date
  sort: parseAsStringLiteral(SORTS).withDefault('-date'),
};

export type ContributionFilters = { member: string; from: string; to: string; sort: (typeof SORTS)[number] };

// Reads the filters from a page's searchParams (a Promise) or from a URLSearchParams
export const loadContributionFilters = createLoader(contributionParsers);