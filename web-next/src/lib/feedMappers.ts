// Turns API rows into small "view" rows. Only what the screen shows crosses into the browser.
export type FeedRow = {
  id: string;
  primary: string;
  secondary: string;
  amountCents: number | null;
  whenUtc: string;
};

export type ApiPage<T> = { items: T[]; nextPageToken: string };

export type ApiContribution = {
  id: string;
  memberName: string;
  amountCents: number;
  status: string;
  recordedAtUtc: string;
};

export type ApiActivity = {
  id: string;
  kind: string;
  stokvelId: string;
  stokvelName: string;
  memberName: string;
  amountCents: number | null;
  occurredAtUtc: string;
};

export function toContributionRow(c: ApiContribution): FeedRow {
  return { id: c.id, primary: c.memberName, secondary: c.status, amountCents: c.amountCents, whenUtc: c.recordedAtUtc };
}

const KIND_LABEL: Record<string, string> = {
  Contribution: 'Contribution',
  Payout: 'Payout',
  MemberJoined: 'Joined',
};

export function toActivityRow(a: ApiActivity): FeedRow {
  return {
    id: `${a.kind}:${a.id}:${a.stokvelId}`, // unique even when the same user joins two stokvels
    primary: a.memberName,
    secondary: `${KIND_LABEL[a.kind] ?? a.kind} · ${a.stokvelName}`,
    amountCents: a.amountCents,
    whenUtc: a.occurredAtUtc,
  };
}