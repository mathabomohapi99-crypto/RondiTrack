import Link from 'next/link';
import { redirect } from 'next/navigation';
import { connection } from 'next/server';
import { Suspense } from 'react';
import { DetailSkeleton } from '@/components/Skeletons';
import { apiJson } from '@/lib/api';
import { formatDate, formatMoney } from '@/lib/format';
import { fetchMembers } from '@/lib/members';
import { centsToInput } from '@/lib/rands';
import { getSession } from '@/lib/session';
import type { StokvelDetail } from '@/lib/types';
import { fetchRecent, RecentContributions, type RecentPage } from './RecentContributions';
import { ProofSection } from './ProofSection';
import { isOfficer } from '@/lib/roles';
import { StokvelActions } from './StokvelActions';

  const showPicker = FORCE_PICKER_FOR_SCREENSHOT || isOfficerHere;

type Params = PageProps<'/stokvels/[id]'>['params'];

// The page returns a <Suspense> at once. The session read and API calls happen inside it.
export default function StokvelDetailPage({ params }: PageProps<'/stokvels/[id]'>) {
  return (
    <Suspense fallback={<DetailSkeleton />}>
      <StokvelDetailView params={params} />
    </Suspense>
  );
}

// A non-member, a missing stokvel and a malformed id all give the API's 404 and apiJson() shows not-found.tsx.
async function StokvelDetailView({ params }: { params: Params }) {
  const { id } = await params; // params is a Promise in Next 16
  await connection(); // "this is per-request from here on" (allows timers and random ids below)
  const startedAt = performance.now();

  // "Recent contributions" starts NOW but is only awaited inside its own <Suspense>, so the top never waits for it.
  const recent: Promise<RecentPage> = fetchRecent(id);
  recent.catch(() => undefined); // an early not-found must not leave an unhandled rejection behind

  // The API already returns the cycle and member count WITH the stokvel (8.1, question 6),
  // so the really independent requests are the stokvel and the members list: they run in parallel.
  const sequential = process.env.SEQUENTIAL_FETCH === '1'; // only for the timing in the README
  let stokvel: StokvelDetail;
  let members: Awaited<ReturnType<typeof fetchMembers>>;
  if (sequential) {
    stokvel = await apiJson<StokvelDetail>(`/stokvels/${id}`);
    members = await fetchMembers(id);
  } else {
    [stokvel, members] = await Promise.all([apiJson<StokvelDetail>(`/stokvels/${id}`), fetchMembers(id)]);
  }
  console.log(
    `[stokvel page] ${sequential ? 'one await per line' : 'parallel'}: ${Math.round(performance.now() - startedAt)} ms`,
  );

  const session = await getSession();
  if (!session) redirect('/login');

  // The Treasurer picker follows THIS stokvel's role from the API's answer. Not the session, not the token.
  // (The API treats Admin like Treasurer for recording, so both see the picker.)
  const isOfficerHere = isOfficer(stokvel.myRole);
  const showPicker = FORCE_PICKER_FOR_SCREENSHOT || isOfficerHere;
  const cycle = stokvel.currentCycle;

  return (
    <div className="space-y-3">
      <h1 className="text-2xl font-bold">{stokvel.name}</h1>
      <p>Contribution: {formatMoney(stokvel.contributionAmount)} ({stokvel.frequency.toLowerCase()})</p>
      <p>Your role here: {stokvel.myRole ?? 'Not a member (platform admin view)'}</p>
      <p>Members: {stokvel.memberCount} of {stokvel.maxMembers}</p>

      <section className="rounded border p-4">
        <h2 className="font-semibold">Current cycle</h2>
        {cycle ? (
          <ul className="mt-1 space-y-1 text-sm">
            <li>Cycle {cycle.cycleNumber}, started {formatDate(cycle.startedAtUtc)}</li>
            <li>Collected: {formatMoney(cycle.collectedAmount)} of {formatMoney(cycle.targetAmount)}</li>
            <li>Next payout: {formatDate(cycle.nextPayoutDateUtc)}</li>
          </ul>
        ) : (
          <p className="text-sm">No open cycle right now.</p>
        )}
      </section>

            <StokvelActions
        stokvel={stokvel}
        meId={session.user.id}
        members={isOfficerHere ? members : []}
        defaultAmount={centsToInput(Math.round(stokvel.contributionAmount * 100))}
        initialKey={crypto.randomUUID()}
      />
      

      {/* NEW 9.1: proof of payment for the current cycle */}
      {cycle && (
        <ProofSection stokvelId={stokvel.id} cycleId={cycle.id} meId={session.user.id} isOfficer={isOfficerHere} />
      )}

      <RecentContributions recent={recent} />

      <p>
        <Link href={`/stokvels/${stokvel.id}/contributions`} className="underline">
          Full contribution history
        </Link>
      </p>
    </div>
  );
}