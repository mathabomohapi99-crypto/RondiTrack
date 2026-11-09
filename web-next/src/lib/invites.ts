import 'server-only';
import { apiFetchWithToken } from './api';
import type { JoinProblem } from './joinFlow';

// Exactly what the API's preview sends. No member names, balances or payout order exist in it.
export type InvitePreview = {
  stokvelName: string;
  contributionAmountCents: number;
  frequency: 'Weekly' | 'Fortnightly' | 'Monthly';
  memberCount: number;
  maxMembers: number;
  expiresAtUtc: string;
  alreadyMember: boolean;
  stokvelId: string | null;
};

export type PreviewOutcome =
  | { kind: 'ok'; preview: InvitePreview }
  | { kind: 'problem'; problem: JoinProblem }
  | { kind: 'signedOut' };

// Asks the API about a code and turns every answer into something a screen can show
export async function fetchInvitePreview(accessToken: string, code: string): Promise<PreviewOutcome> {
  let res: Response;
  try {
    res = await apiFetchWithToken(accessToken, `/invites/${encodeURIComponent(code)}`);
  } catch {
    return { kind: 'problem', problem: 'unavailable' };
  }
  if (res.ok) {
    const b = (await res.json()) as InvitePreview;
    return {
      kind: 'ok',
      preview: {
        stokvelName: b.stokvelName,
        contributionAmountCents: b.contributionAmountCents,
        frequency: b.frequency,
        memberCount: b.memberCount,
        maxMembers: b.maxMembers,
        expiresAtUtc: b.expiresAtUtc,
        alreadyMember: b.alreadyMember,
        stokvelId: b.stokvelId,
      },
    };
  }
  if (res.status === 401) return { kind: 'signedOut' };
  if (res.status === 404) return { kind: 'problem', problem: 'not_found' };
  if (res.status === 410) return { kind: 'problem', problem: 'expired' };
  if (res.status === 429) return { kind: 'problem', problem: 'busy' };
  return { kind: 'problem', problem: 'unavailable' };
}