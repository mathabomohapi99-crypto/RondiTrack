// CHANGED 7.2: hard-coded sample data in the real API shapes. Made-up names only.
// 2026-10-31T21:59:59Z is 23:59:59 on 31 Oct in South Africa. 2026-10-31T22:30:00Z is 00:30 on 1 Nov.
import type { ContributionItem, CycleItem, HomeStokvelEntry } from '@/api/types'

const STOKVEL_A = '3f1c2a10-0000-4000-8000-000000000001'
const CYCLE_1 = '3f1c2a10-0000-4000-8000-0000000000a1'
const CYCLE_2 = '3f1c2a10-0000-4000-8000-0000000000a2'
const CYCLE_3 = '3f1c2a10-0000-4000-8000-0000000000a3'
const LONG_SURNAME = 'Thandiwe Nkosi-Mkhwanazi-Van Rensburg'

// Lerato: Treasurer in one stokvel (long name, all paid up), Member in another (owes money, in arrears)
export const home: HomeStokvelEntry[] = [
  {
    stokvelId: STOKVEL_A,
    name: 'Mokoena Family Burial and Savings Society (Soweto)',
    role: 'Treasurer',
    currentCycle: {
      id: CYCLE_3,
      number: 3,
      status: 'Open',
      startsAt: '2026-09-30T22:00:00Z',
      endsAt: '2026-10-31T21:59:59Z',
    },
    paidCents: 50000,
    owedCents: 0, // all paid up
    nextPayoutAt: '2026-11-07T08:00:00Z',
    arrearsCents: null, // no arrears = no badge
  },
  {
    stokvelId: '3f1c2a10-0000-4000-8000-000000000002',
    name: 'Festive Grocery Club',
    role: 'Member',
    currentCycle: {
      id: '3f1c2a10-0000-4000-8000-0000000000b2',
      number: 2,
      status: 'Open',
      startsAt: '2026-10-14T22:00:00Z',
      endsAt: '2026-11-13T21:59:59Z',
    },
    paidCents: 0,
    owedCents: 25000, // still owes this cycle
    nextPayoutAt: null, // no payout scheduled
    arrearsCents: 25000, // in arrears from the last cycle
  },
]

// The cycles of the first stokvel, newest first. Totals come from the API: the browser never adds anything up.
export const cycles: CycleItem[] = [
  { id: CYCLE_3, number: 3, status: 'Open', startsAt: '2026-09-30T22:00:00Z', endsAt: '2026-10-31T21:59:59Z', collectedCents: 185060, dueCents: 250000 },
  { id: CYCLE_2, number: 2, status: 'Closed', startsAt: '2026-08-31T22:00:00Z', endsAt: '2026-09-30T21:59:59Z', collectedCents: 0, dueCents: 250000 },
  { id: CYCLE_1, number: 1, status: 'PaidOut', startsAt: '2026-07-31T22:00:00Z', endsAt: '2026-08-31T21:59:59Z', collectedCents: 250000, dueCents: 250000 },
]

// Contributions: all three statuses, a 10 cent one, the midnight one, a long double-barrelled name,
// and cycle 2 has none (shows "No contributions yet")
export const contributions: ContributionItem[] = [
  { id: '7a000000-0000-4000-8000-000000000001', cycleId: CYCLE_3, userId: '5e000000-0000-4000-8000-000000000001', memberName: LONG_SURNAME, role: 'Treasurer', rotationPosition: 1, amountCents: 50000, status: 'Confirmed', recordedAtUtc: '2026-10-05T08:15:00Z' },
  { id: '7a000000-0000-4000-8000-000000000002', cycleId: CYCLE_3, userId: '5e000000-0000-4000-8000-000000000002', memberName: 'Sipho Dlamini', role: 'Member', rotationPosition: 2, amountCents: 15050, status: 'Paid', recordedAtUtc: '2026-10-12T10:00:00Z' },
  { id: '7a000000-0000-4000-8000-000000000003', cycleId: CYCLE_3, userId: '5e000000-0000-4000-8000-000000000003', memberName: 'Lerato Mokoena', role: 'Member', rotationPosition: 3, amountCents: 20000, status: 'Pending', recordedAtUtc: '2026-10-14T09:30:00Z' },
  { id: '7a000000-0000-4000-8000-000000000004', cycleId: CYCLE_3, userId: '5e000000-0000-4000-8000-000000000004', memberName: 'Kagiso Molefe', role: 'Member', rotationPosition: 4, amountCents: 100000, status: 'Paid', recordedAtUtc: '2026-10-31T22:30:00Z' }, // the midnight case
  { id: '7a000000-0000-4000-8000-000000000005', cycleId: CYCLE_3, userId: '5e000000-0000-4000-8000-000000000005', memberName: 'Naledi Khumalo', role: 'Member', rotationPosition: 5, amountCents: 10, status: 'Paid', recordedAtUtc: '2026-10-20T07:45:00Z' }, // 10 cents
  { id: '7a000000-0000-4000-8000-000000000006', cycleId: CYCLE_1, userId: '5e000000-0000-4000-8000-000000000001', memberName: LONG_SURNAME, role: 'Treasurer', rotationPosition: 1, amountCents: 100000, status: 'Confirmed', recordedAtUtc: '2026-08-05T08:00:00Z' },
  { id: '7a000000-0000-4000-8000-000000000007', cycleId: CYCLE_1, userId: '5e000000-0000-4000-8000-000000000002', memberName: 'Sipho Dlamini', role: 'Member', rotationPosition: 2, amountCents: 150000, status: 'Confirmed', recordedAtUtc: '2026-08-07T08:00:00Z' },
]