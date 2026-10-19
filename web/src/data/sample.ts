// NEW: sample data in the real shape of /me/home. Made-up names only.
// 2026-10-31T21:59:59Z is 23:59:59 on 31 Oct in South Africa.
import type { HomeStokvelEntry } from '../api/types'

// Lerato: Treasurer in one stokvel, ordinary Member in another, with the same login
export const home: HomeStokvelEntry[] = [
  {
    stokvelId: '3f1c2a10-0000-4000-8000-000000000001',
    name: 'Family Savings Stokvel',
    role: 'Treasurer',
    currentCycle: {
      id: '3f1c2a10-0000-4000-8000-0000000000c1',
      status: 'Open',
      startsAt: '2026-09-30T22:00:00Z',
      endsAt: '2026-10-31T21:59:59Z',
    },
    paidCents: 50000,
    owedCents: 150000, // still owes money this cycle
    nextPayoutAt: '2026-11-07T08:00:00Z',
  },
  {
    stokvelId: '3f1c2a10-0000-4000-8000-000000000002',
    name: 'Work Savings Circle',
    role: 'Member',
    currentCycle: {
      id: '3f1c2a10-0000-4000-8000-0000000000c2',
      status: 'Open',
      startsAt: '2026-09-30T22:00:00Z',
      endsAt: '2026-10-31T21:59:59Z',
    },
    paidCents: 20000,
    owedCents: 0, // all paid up
    nextPayoutAt: null, // no payout scheduled
  },
]