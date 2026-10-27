// The shapes the API returns (they match API/Dtos/MeDtos.cs). Money is in rands (decimal), not cents.

export type Role = 'Member' | 'Treasurer' | 'Admin';

export type CycleSummary = {
  id: string;
  cycleNumber: number;
  targetAmount: number;
  collectedAmount: number;
  startedAtUtc: string;
  nextPayoutDateUtc: string;
};

// One row of GET /api/me/stokvels. "myRole" comes from the API for THIS stokvel, never from the token.
export type MyStokvel = {
  id: string;
  name: string;
  contributionAmount: number;
  frequency: 'Weekly' | 'Fortnightly' | 'Monthly';
  memberCount: number;
  myRole: Role;
  paidThisCycle: number;
  owedThisCycle: number;
  nextPayoutDateUtc: string | null;
  currentCycle: CycleSummary | null;
};

// GET /api/stokvels/{id}
export type StokvelDetail = {
  id: string;
  name: string;
  contributionAmount: number;
  frequency: 'Weekly' | 'Fortnightly' | 'Monthly';
  maxMembers: number;
  memberCount: number;
  isFull: boolean;
  payoutPerCycle: number;
  createdAtUtc: string;
  myRole: Role | null;
  currentCycle: CycleSummary | null;
};