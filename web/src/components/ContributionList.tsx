// NEW 7.2: contributions grouped under their cycle, newest cycle first.
// The grouping is a pure function called while rendering. The ONLY state is which groups are open,
// kept here in the list (not in each group) because both a group's own toggle and "Expand all" need it,
// and keyed by cycle id (not position) so a new cycle shifting the list never opens the wrong group.
import { useState } from 'react'
import { ChevronDown } from 'lucide-react'
import type { ContributionItem, CycleItem } from '@/api/types'
import { ContributionCard } from '@/components/ContributionCard'
import { DueCountdown } from '@/components/DueCountdown'
import { StatusBadge } from '@/components/StatusBadge'
import { Button } from '@/components/ui/button'
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible'
import { formatDate } from '@/lib/dates'
import { formatCents } from '@/lib/money'
import { groupContributionsByCycle, type CycleGroup } from '@/lib/groupContributionsByCycle'

type Group = CycleGroup<CycleItem, ContributionItem>

export type ContributionListProps = {
  cycles: CycleItem[]
  contributions: ContributionItem[]
}

// Starts open: the Open cycle, and the Unknown group (it may hold money nobody has accounted for)
function startsOpen(group: Group): boolean {
  return group.cycle === null || group.cycle.status === 'Open'
}

export function ContributionList({ cycles, contributions }: ContributionListProps) {
  const groups = groupContributionsByCycle(cycles, contributions) // derived, not state
  const [openKeys, setOpenKeys] = useState<ReadonlySet<string>>(
    () => new Set(groups.filter(startsOpen).map((group) => group.key)),
  )

  // The button label is worked out from the open groups, not stored
  const allOpen = groups.every((group) => openKeys.has(group.key))

  function toggle(key: string) {
    setOpenKeys((previous) => {
      const next = new Set(previous)
      if (next.has(key)) next.delete(key)
      else next.add(key)
      return next
    })
  }

  function setAll(open: boolean) {
    setOpenKeys(open ? new Set(groups.map((group) => group.key)) : new Set())
  }

  if (groups.length === 0) return <p>No cycles yet</p>

  return (
    <div className="flex flex-col gap-3">
      <div>
        <Button type="button" variant="outline" size="sm" onClick={() => setAll(!allOpen)}>
          {allOpen ? 'Collapse all' : 'Expand all'}
        </Button>
      </div>

      {groups.map((group) => {
        const cycle = group.cycle
        return (
          <div key={group.key} className="min-w-0 rounded-lg border bg-card text-card-foreground">
            <Collapsible open={openKeys.has(group.key)} onOpenChange={() => toggle(group.key)}>
              {/* the header is a real button (Radix sets aria-expanded) inside a real heading */}
              <h3>
                <CollapsibleTrigger className="group flex min-h-11 w-full items-center justify-between gap-2 rounded-lg px-3 py-2 text-left text-base font-semibold">
                  <span className="min-w-0 break-words">{cycle ? `Cycle ${cycle.number}` : 'Unknown cycle'}</span>
                  <span className="flex shrink-0 items-center gap-2">
                    {cycle && <StatusBadge kind="cycle" status={cycle.status} />}
                    <ChevronDown
                      aria-hidden="true"
                      className="size-4 shrink-0 transition-transform group-data-[state=open]:rotate-180"
                    />
                  </span>
                </CollapsibleTrigger>
              </h3>

              {cycle ? (
                <div className="flex flex-col gap-1 px-3 pb-3 text-sm">
                  <p>Due {formatDate(cycle.endsAt)}</p>
                  <DueCountdown due={cycle.endsAt} cycleStatus={cycle.status} />
                  <p>
                    Collected <span className="whitespace-nowrap font-medium">{formatCents(cycle.collectedCents)}</span> of{' '}
                    <span className="whitespace-nowrap font-medium">{formatCents(cycle.dueCents)}</span>
                  </p>
                </div>
              ) : (
                <p className="px-3 pb-3 text-sm">These contributions belong to a cycle that is not in the cycles list.</p>
              )}

              <CollapsibleContent>
                <div className="flex flex-col gap-2 px-3 pb-3">
                  {group.contributions.length > 0 ? (
                    group.contributions.map((contribution) => (
                      <ContributionCard key={contribution.id} {...contribution} />
                    ))
                  ) : (
                    <p className="text-sm text-muted-foreground">No contributions yet</p>
                  )}
                </div>
              </CollapsibleContent>
            </Collapsible>
          </div>
        )
      })}
    </div>
  )
}