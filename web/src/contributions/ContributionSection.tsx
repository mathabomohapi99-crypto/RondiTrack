// NEW 7.4: shows the form only on an OPEN cycle (otherwise a sentence saying why) and works out who is who.
import { useHome, useMembers } from '../api/queries';
import { currentUserId } from '../auth/session';
import { ErrorView } from '../components/ErrorView';
import { Skeleton } from '../components/Skeleton';
import { isTreasurer } from '../lib/roles';
import { ContributionForm, type Member } from './ContributionForm';

type Props = {
  stokvelId: string;
  cycle: { id: string; number: number; status: string };
};

// Reads a member's name without hand-writing the API type (the API calls it fullName)
function toMember(u: unknown): Member | null {
  const o = u as Record<string, unknown>;
  const id = typeof o.id === 'string' ? o.id : typeof o.userId === 'string' ? o.userId : null;
  const name = [o.fullName, o.name, o.email].find((v): v is string => typeof v === 'string');
  return id ? { id, name: name ?? id } : null;
}

export function ContributionSection({ stokvelId, cycle }: Props) {
  const home = useHome();
  const mine = home.data?.stokvels.find((s) => s.stokvelId === stokvelId);
  const treasurer = mine ? isTreasurer(mine.role) : false;
  const membersQuery = useMembers(stokvelId, treasurer);

  // Closed or PaidOut: no form, just the reason
  if (cycle.status !== 'Open') {
    return (
      <p>
        Cycle {cycle.number} is {cycle.status === 'PaidOut' ? 'paid out' : 'closed'}, so contributions can no longer be
        recorded.
      </p>
    );
  }

  if (home.isPending) return <Skeleton label="Loading contribution form" />;
  if (home.isError) return <ErrorView error={home.error} onRetry={() => void home.refetch()} />;
  if (!mine) return <p>You are not a member of this stokvel, so you cannot record a contribution here.</p>;

  const meId = currentUserId();
  if (!meId) return <p>Please sign in again to record a contribution.</p>;

  if (treasurer && membersQuery.isPending) return <Skeleton label="Loading members" />;
  if (treasurer && membersQuery.isError) {
    return <ErrorView error={membersQuery.error} onRetry={() => void membersQuery.refetch()} />;
  }

  const members = (membersQuery.data?.pages.flatMap((p) => p.items) ?? [])
    .map(toMember)
    .filter((m): m is Member => m !== null);

  return (
    <ContributionForm
      stokvelId={stokvelId}
      cycleId={cycle.id}
      cycleNumber={cycle.number}
      isTreasurer={treasurer}
      meId={meId}
      members={members}
      owedCents={mine.owedCents}
    />
  );
}