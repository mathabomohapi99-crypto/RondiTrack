// The shapes the API returns. Money is in rands (decimal) here, in integer cents in history/activity rows.

export type Role = 'Member' | 'Treasurer' | 'Admin';

export type CycleSummary = {
  id: string;
  cycleNumber: number;
  targetAmount: number;
  collectedAmount: number;
  startedAtUtc: string;
  nextPayoutDateUtc: string;
};

// One row of GET /api/v1/me/stokvels. "myRole" comes from the API for THIS stokvel, never from the token.
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

// GET /api/v1/stokvels/{id}
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

// NEW 8.2: only what a Client Component needs about a member: id and name.
export type MemberOption = { id: string; name: string };