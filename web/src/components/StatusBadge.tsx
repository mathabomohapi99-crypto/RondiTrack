// NEW: one badge for every RondiTrack status. It takes a KIND as well as a status, because
// "Paid" (a member's claim) and "PaidOut" (the whole pot went out) must never look alike.
// Every badge shows a WORD + an ICON + a tone: never colour alone (WCAG 1.4.1).
import {
  Banknote, Check, CheckCheck, CircleDot, Clock, Info, Lock, TriangleAlert, type LucideIcon,
} from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { cn } from '@/lib/utils'
import { formatCents } from '@/lib/money'

export type StatusKind = 'cycle' | 'contribution'

// Light + dark colour pairs chosen for 4.5:1 text contrast. Measure them in DevTools (README table).
const TONE = {
  indigo: 'border-indigo-300 bg-indigo-100 text-indigo-900 dark:border-indigo-800 dark:bg-indigo-950 dark:text-indigo-200',
  slate: 'border-slate-300 bg-slate-100 text-slate-900 dark:border-slate-600 dark:bg-slate-800 dark:text-slate-100',
  violet: 'border-violet-300 bg-violet-100 text-violet-900 dark:border-violet-800 dark:bg-violet-950 dark:text-violet-200',
  amber: 'border-amber-300 bg-amber-100 text-amber-900 dark:border-amber-800 dark:bg-amber-950 dark:text-amber-200',
  sky: 'border-sky-300 bg-sky-100 text-sky-900 dark:border-sky-800 dark:bg-sky-950 dark:text-sky-200',
  green: 'border-green-300 bg-green-100 text-green-900 dark:border-green-800 dark:bg-green-950 dark:text-green-200',
  red: 'border-red-300 bg-red-100 text-red-900 dark:border-red-800 dark:bg-red-950 dark:text-red-200',
  neutral: 'border-border bg-muted text-foreground', // theme tokens for the "unknown" look
}

type Look = { label: string; Icon: LucideIcon; tone: string }

const CYCLE_LOOK = new Map<string, Look>([
  ['Open', { label: 'Open', Icon: CircleDot, tone: TONE.indigo }],
  ['Closed', { label: 'Closed', Icon: Lock, tone: TONE.slate }],
  ['PaidOut', { label: 'Paid out', Icon: Banknote, tone: TONE.violet }], // human words, not "PaidOut"
])

const CONTRIBUTION_LOOK = new Map<string, Look>([
  ['Pending', { label: 'Pending', Icon: Clock, tone: TONE.amber }],
  ['Paid', { label: 'Paid, awaiting check', Icon: Check, tone: TONE.sky }], // the member's claim
  ['Confirmed', { label: 'Confirmed', Icon: CheckCheck, tone: TONE.green }], // the treasurer's check
])

export function StatusBadge({ kind, status }: { kind: StatusKind; status: string }) {
  const table = kind === 'cycle' ? CYCLE_LOOK : CONTRIBUTION_LOOK
  // An unknown status shows its raw value in a neutral badge: no crash, no guess
  const look = table.get(status) ?? { label: status, Icon: Info, tone: TONE.neutral }
  const { label, Icon, tone } = look
  return (
    <Badge variant="outline" className={cn('h-auto gap-1 whitespace-normal text-left', tone)}>
      <Icon aria-hidden="true" className="size-3.5 shrink-0" />
      {label}
    </Badge>
  )
}

// Arrears badge: shows only when the caller really owes arrears. No arrears means no badge at all.
export function ArrearsBadge({ cents }: { cents: number | null | undefined }) {
  if (cents === null || cents === undefined || cents <= 0) return null
  return (
    <Badge variant="outline" className={cn('h-auto gap-1 whitespace-normal text-left', TONE.red)}>
      <TriangleAlert aria-hidden="true" className="size-3.5 shrink-0" />
      <span>
        In arrears: <span className="whitespace-nowrap">{formatCents(cents)}</span>
      </span>
    </Badge>
  )
}