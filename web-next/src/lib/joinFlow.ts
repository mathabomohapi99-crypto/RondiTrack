// NEW 9.1: the join flow as ONE small state machine (no booleans scattered through components).
export const STEPS = ['code', 'rules', 'confirm'] as const;
export type Step = (typeof STEPS)[number];
export type Screen = Step | 'member'; // "member" = the "you are already in" screen (not a step)
export type JoinEvent = 'codeAccepted' | 'rulesRead' | 'back' | 'alreadyMember';
export type JoinProblem = 'not_found' | 'expired' | 'busy' | 'unavailable' | 'bad_format';

// The table: where each event moves each step
const TABLE: Record<Step, Partial<Record<JoinEvent, Screen>>> = {
  code: { codeAccepted: 'rules' },
  rules: { rulesRead: 'confirm', back: 'code', alreadyMember: 'member' },
  confirm: { back: 'rules', alreadyMember: 'member' },
};

export function transition(from: Step, event: JoinEvent): Screen {
  return TABLE[from][event] ?? from;
}

export function parseScreen(value: string | undefined): Screen {
  return value === 'rules' || value === 'confirm' || value === 'member' ? value : 'code';
}

export function stepNumber(step: Step): number {
  return STEPS.indexOf(step) + 1;
}

export const CODE_LENGTH = 8;

// "k7m2 9qxp" and "K7M2-9QXP" are the same code. Anything that is not 8 letters/digits is not a code.
export function normalizeCode(raw: string): string | null {
  const code = raw.replace(/[^a-zA-Z0-9]/g, '').toUpperCase();
  return code.length === CODE_LENGTH ? code : null;
}

// The ONE place that builds a join URL, so the URL always says which screen it is
export function joinHref(screen: Screen, code: string | null, problem?: JoinProblem): string {
  const qs = new URLSearchParams();
  if (code) qs.set('code', code);
  qs.set('step', screen);
  if (problem) qs.set('problem', problem);
  return `/join?${qs.toString()}`;
}

const PROBLEMS: readonly JoinProblem[] = ['not_found', 'expired', 'busy', 'unavailable', 'bad_format'];
export function parseProblem(value: string | undefined): JoinProblem | null {
  return PROBLEMS.find((p) => p === value) ?? null;
}

// Every answer is a screen with words, never an error page
export const PROBLEM_TEXT: Record<JoinProblem, string> = {
  not_found: 'Check the code and try again.',
  expired: 'That invite has expired. Ask for a new invite.',
  busy: 'Too many attempts. Please wait a minute and try again.',
  unavailable: 'We could not check that code right now. Please try again.',
  bad_format: 'An invite code has 8 letters and numbers. Check the code and try again.',
};

export type JoinState = { error?: string; correlationId?: string };