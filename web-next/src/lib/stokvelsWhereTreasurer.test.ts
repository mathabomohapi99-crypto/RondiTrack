import { describe, expect, it } from 'vitest';
import { stokvelsWhereTreasurer } from './stokvelsWhereTreasurer';
import type { MyStokvel, Role } from './types';

// Small helper to build a test stokvel.
const make = (id: string, myRole: Role): MyStokvel => ({
  id,
  name: `Stokvel ${id}`,
  contributionAmount: 100,
  frequency: 'Monthly',
  memberCount: 3,
  myRole,
  paidThisCycle: 0,
  owedThisCycle: 100,
  nextPayoutDateUtc: null,
  currentCycle: null,
});

describe('stokvelsWhereTreasurer', () => {
  it('keeps only the Treasurer stokvels in a mixed list', () => {
    const result = stokvelsWhereTreasurer([make('1', 'Treasurer'), make('2', 'Member'), make('3', 'Treasurer')]);
    expect(result.map((s) => s.id)).toEqual(['1', '3']);
  });

  it('returns an empty list when the person is Treasurer nowhere', () => {
    expect(stokvelsWhereTreasurer([make('1', 'Member'), make('2', 'Member')])).toEqual([]);
  });

  it('returns an empty list for an empty list', () => {
    expect(stokvelsWhereTreasurer([])).toEqual([]);
  });
});