export const PEOPLE = ['sipho', 'thandi', 'kagiso'] as const;
export type Person = (typeof PEOPLE)[number];

export const EMAIL: Record<Person, string> = {
  sipho: 'sipho.e2e@example.com',
  thandi: 'thandi.e2e@example.com',
  kagiso: 'kagiso.e2e@example.com',
};

// Same ids as API/Jobs/E2eSeed.cs. Sipho: treasurer of FAMILY, only a member of WORK.
export const FAMILY = 'a0000000-0000-0000-0000-000000000001';
export const WORK = 'a0000000-0000-0000-0000-000000000002';
export const PERF = 'a0000000-0000-0000-0000-000000000003';

export const authFile = (person: Person) => `e2e/.auth/${person}.json`;