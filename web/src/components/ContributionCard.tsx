// CHANGED 7.2: one contribution, styled. The cycle is shown by the group header in ContributionList.
// The member's name is shown because every member of a stokvel can already list its members and contributions.
import type { ContributionItem } from '@/api/types'
import { StatusBadge } from '@/components/StatusBadge'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { formatDateTime } from '@/lib/dates'
import { formatCents } from '@/lib/money'

export type ContributionCardProps = ContributionItem

export function ContributionCard({ memberName, amountCents, status, recordedAtUtc }: ContributionCardProps) {
  return (
    <Card className="min-w-0 gap-2 py-3">
      <CardHeader className="px-4">
        <CardTitle>
          <h4 className="break-words text-base leading-snug">{memberName}</h4>
        </CardTitle>
      </CardHeader>
      <CardContent className="flex flex-wrap items-center justify-between gap-2 px-4 text-sm">
        <span className="whitespace-nowrap text-base font-semibold">{formatCents(amountCents)}</span>
        <StatusBadge kind="contribution" status={status} />
        <span className="basis-full text-muted-foreground">Recorded {formatDateTime(recordedAtUtc)}</span>
      </CardContent>
    </Card>
  )
}