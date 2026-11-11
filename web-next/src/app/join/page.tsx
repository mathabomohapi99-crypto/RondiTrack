import Link from 'next/link';
import { redirect } from 'next/navigation';
import { Suspense } from 'react';
import { DetailSkeleton } from '@/components/Skeletons';
import { formatCents, formatDate } from '@/lib/format';
import { fetchInvitePreview, type InvitePreview } from '@/lib/invites';
import {joinHref, normalizeCode, parseProblem, parseScreen, PROBLEM_TEXT, stepNumber, STEPS,type JoinProblem, type Screen, type Step,} from '@/lib/joinFlow';
import { getSession } from '@/lib/session';
import { FocusHeading } from './FocusHeading';
import { JoinForm } from './JoinForm';
import { checkCode } from './joinActions';
import type { Metadata } from 'next';
import { isPreviewBot } from '@/lib/previewBot';

type Props = PageProps<'/join'>;
// The ONLY generateMetadata in the app. Signed out, nobody can know if the invite is valid (the preview needs a sign-in, and a public
// "is this code valid?" endpoint would let anyone guess codes), so it says the same generic thing for every invite: no name, no amount.
export async function generateMetadata(): Promise<Metadata> {
  const title = 'You are invited to a stokvel on RondiTrack';
  const description = 'Open this invite on RondiTrack, sign in, and see the details before you join.';
  return { title, description, robots: { index: false, follow: false }, openGraph: { title, description, siteName: 'RondiTrack' } };
}

// The URL decides the step: /join?code=...&step=rules. Reload, Back and the invite link itself all land on the right one.
export default function JoinPage({ searchParams }: Props) {
  return (
        <main className="mx-auto max-w-lg space-y-4 p-6">
      <Suspense fallback={<DetailSkeleton />}>
        <JoinBody searchParams={searchParams} />
      </Suspense>
    </main>
  );
}

async function JoinBody({ searchParams }: { searchParams: Props['searchParams'] }) {
  const sp = await searchParams;
  const rawCode = typeof sp.code === 'string' ? sp.code.trim().slice(0, 40) : '';
  const code = normalizeCode(rawCode);
  const rawStep = typeof sp.step === 'string' ? sp.step : undefined;
  // An invite link has only ?code=: it lands on the rules. No code at all: step 1.
  const screen: Screen = rawStep ? parseScreen(rawStep) : code ? 'rules' : 'code';
  const problem = parseProblem(typeof sp.problem === 'string' ? sp.problem : undefined);

  if (screen === 'code') return <CodeStep rawCode={rawCode} problem={problem} />;

  // A step the person cannot be on yet (no valid code) redirects, so the URL never says one thing and shows another
  if (code === null) redirect(joinHref('code', rawCode || null, rawCode ? 'bad_format' : undefined));

  const session = await getSession();
  if (!session) {
    // A preview bot gets a plain page (so it reads the metadata). A person is sent to sign-in and comes back to the same step.
    if (await isPreviewBot()) return <SignInPrompt next={joinHref(screen, code)} />;
    redirect(`/login?next=${encodeURIComponent(joinHref(screen, code))}`);
  }

  const outcome = await fetchInvitePreview(session.accessToken, code);
  if (outcome.kind === 'signedOut') redirect('/login?expired=1');
  if (outcome.kind === 'problem') redirect(joinHref('code', code, outcome.problem)); // unknown or expired: back to step 1 with words
  const { preview } = outcome;

  if (screen === 'member') {
    if (!preview.alreadyMember) redirect(joinHref('rules', code));
    return <AlreadyMember preview={preview} />;
  }
  if (preview.alreadyMember) redirect(joinHref('member', code));

  return screen === 'rules' ? (
    <RulesStep code={code} preview={preview} />
  ) : (
    <ConfirmStep code={code} preview={preview} />
  );
}

const LABELS: Record<Step, string> = { code: 'Invite code', rules: 'What you are joining', confirm: 'Confirm' };

// The step list. aria-current="step" marks where you are.
function StepList({ current }: { current: Step }) {
  return (
    <ol aria-label="Steps to join a stokvel" className="flex flex-wrap gap-4 text-sm">
      {STEPS.map((s) => (
        <li key={s} aria-current={s === current ? 'step' : undefined}
          className={s === current ? 'font-bold underline' : 'text-gray-600'}>
          {stepNumber(s)}. {LABELS[s]}
        </li>
      ))}
    </ol>
  );
}

function CodeStep({ rawCode, problem }: { rawCode: string; problem: JoinProblem | null }) {
  return (
    <>
      <StepList current="code" />
      <FocusHeading key="code">Step 1 of 3: Enter your invite code</FocusHeading>
      {problem && (
        <p role="alert" className="rounded border border-amber-500 p-3 text-sm">{PROBLEM_TEXT[problem]}</p>
      )}
      <form action={checkCode} className="space-y-3">
        <div>
          <label htmlFor="code" className="block text-sm">Invite code</label>
          {/* Prefilled from the URL, so Back never asks for it again (WCAG 3.3.7). Paste works. */}
          <input id="code" name="code" defaultValue={rawCode} autoComplete="off" autoCapitalize="characters"
            spellCheck={false} maxLength={40} aria-invalid={problem ? true : undefined}
            className="w-full rounded border p-2 font-mono uppercase" />
        </div>
        <button type="submit" className="rounded bg-black px-4 py-2 text-white">Continue</button>
      </form>
    </>
  );
}

// What the person needs to decide. Nothing about members.
function Details({ preview }: { preview: InvitePreview }) {
  const full = preview.memberCount >= preview.maxMembers;
  return (
    <>
      <dl className="space-y-1 rounded border p-4 text-sm">
        <div><dt className="inline font-medium">Stokvel: </dt><dd className="inline">{preview.stokvelName}</dd></div>
        <div>
          <dt className="inline font-medium">Contribution: </dt>
          <dd className="inline">{formatCents(preview.contributionAmountCents)} {preview.frequency.toLowerCase()}</dd>
        </div>
        <div><dt className="inline font-medium">Members: </dt><dd className="inline">{preview.memberCount} of {preview.maxMembers}</dd></div>
        <div><dt className="inline font-medium">Invite valid until: </dt><dd className="inline">{formatDate(preview.expiresAtUtc)}</dd></div>
      </dl>
      {full && <p role="status" className="text-sm text-amber-700">This stokvel is full right now, so joining may be refused.</p>}
    </>
  );
}

const linkButton = 'inline-block rounded border px-4 py-2';

function RulesStep({ code, preview }: { code: string; preview: InvitePreview }) {
  return (
    <>
      <StepList current="rules" />
      <FocusHeading key="rules">Step 2 of 3: What you are joining</FocusHeading>
      <Details preview={preview} />
      <div className="flex gap-3">
        <Link href={joinHref('code', code)} className={linkButton}>Back</Link>
        <Link href={joinHref('confirm', code)} className="inline-block rounded bg-black px-4 py-2 text-white">Continue</Link>
      </div>
    </>
  );
}

function ConfirmStep({ code, preview }: { code: string; preview: InvitePreview }) {
  return (
    <>
      <StepList current="confirm" />
      <FocusHeading key="confirm">Step 3 of 3: Confirm and join</FocusHeading>
      {/* Shown again from the invite, so the person is never asked for anything twice */}
      <Details preview={preview} />
      <JoinForm code={code} stokvelName={preview.stokvelName} />
      <Link href={joinHref('rules', code)} className={linkButton}>Back</Link>
    </>
  );
}

function AlreadyMember({ preview }: { preview: InvitePreview }) {
  return (
    <>
      <FocusHeading key="member">You are already a member of {preview.stokvelName}</FocusHeading>
      {preview.stokvelId && (
        <Link href={`/stokvels/${preview.stokvelId}`} className="inline-block rounded bg-black px-4 py-2 text-white">
          Open {preview.stokvelName}
        </Link>
function SignInPrompt({ next }: { next: string }) {
  return (
    <>
      <FocusHeading key="signin">You are invited to a stokvel on RondiTrack</FocusHeading>
      <p className="text-sm">Sign in to see the details and join.</p>
      <Link href={`/login?next=${encodeURIComponent(next)}`} className="inline-block rounded bg-black px-4 py-2 text-white">
        Sign in
      </Link>
    </>
  );
}

      )}
    </>
  );
}