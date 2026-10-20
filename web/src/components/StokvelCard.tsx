// CHANGED 7.2: one stokvel, styled with shadcn Card. The role still comes ONLY from this stokvel's props.
import type { HomeStokvelEntry } from '@/api/types'
import { DueCountdown } from '@/components/DueCountdown'
import { ArrearsBadge, StatusBadge } from '@/components/StatusBadge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { formatDate } from '@/lib/dates'
import { formatCents } from '@/lib/money'
import { isTreasurer, roleLabel } from '@/lib/roles'

export type StokvelCardProps = HomeStokvelEntry

export function StokvelCard({
  name,
  role,
  currentCycle,
  paidCents,
  owedCents,
  nextPayoutAt,
  arrearsCents,
}: StokvelCardProps) {
  // Derived while rendering, not stored in state
  const allPaidUp = owedCents === 0

  return (
    <Card className="min-w-0">
      <CardHeader>
        {/* a real heading inside CardTitle; long names wrap instead of pushing the card wider */}
        <CardTitle>
          <h3 className="break-words text-lg leading-snug">{name}</h3>
        </CardTitle>
        {/* role as words, never a coloured badge */}
        <p className="font-medium">{roleLabel(role)}</p>
      </CardHeader>

      <CardContent className="flex flex-col gap-3 text-sm">
        {!currentCycle ? (
          <p>No open cycle</p>
        ) : (
          <>
            <div className="flex flex-wrap items-center gap-2">
              <span className="font-medium">Cycle {currentCycle.number}</span>
              <StatusBadge kind="cycle" status={currentCycle.status} />
            </div>
            <p>Due {formatDate(currentCycle.endsAt)}</p>
            <DueCountdown due={currentCycle.endsAt} cycleStatus={currentCycle.status} />
            <p>
              Paid this cycle: <span className="whitespace-nowrap font-medium">{formatCents(paidCents)}</span>
            </p>
            {allPaidUp ? (
              <p>All paid up for this cycle</p>
            ) : (
              <p>
                Still owed: <span className="whitespace-nowrap font-medium">{formatCents(owedCents)}</span>
              </p>
            )}
          </>
        )}

        {/* the caller's OWN arrears only. No arrears means no badge at all. */}
        <div>
          <ArrearsBadge cents={arrearsCents} />
        </div>

        {nextPayoutAt ? <p>Next payout: {formatDate(nextPayoutAt)}</p> : <p>No payout scheduled</p>}

        {/* Only a Treasurer gets this. Hiding is a convenience, not security: the API still returns 403 to a Member. */}
        {isTreasurer(role) && (
          <section aria-label="Treasurer jobs" className="rounded-md border border-dashed p-3">
            <h4 className="mb-2 font-semibold">Treasurer jobs</h4>
            <div className="flex flex-wrap gap-2">
              <Button type="button" variant="outline" disabled>
                Close cycle
              </Button>
              <Button type="button" variant="outline" disabled>
                Record payout
              </Button>
            </div>
            <p className="mt-2 text-muted-foreground">Coming in Week 8</p>
          </section>
        )}
      </CardContent>
    </Card>
  )
}