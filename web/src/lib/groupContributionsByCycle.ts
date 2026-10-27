// NEW: groups contributions under their cycle. A pure function: no state, no effects, called during render.
// Generic on purpose: it only needs a cycle id + number and a contribution cycleId, so the real types come from the API.
export const UNKNOWN_CYCLE_KEY = 'unknown-cycle'

export type CycleGroup<C, T> = {
  key: string // the cycle id (or "unknown-cycle"): open/closed state is keyed by this, never by position
  cycle: C | null // null only for the "Unknown cycle" group
  contributions: T[]
}

export function groupContributionsByCycle<
  C extends { id: string; number: number },
  T extends { cycleId: string },
>(cycles: readonly C[], contributions: readonly T[]): CycleGroup<C, T>[] {
  const knownIds = new Set(cycles.map((cycle) => cycle.id))
  const byCycle = new Map<string, T[]>()
  const orphans: T[] = []

  // Keep each contribution in the order the API gave it (newest first)
  for (const contribution of contributions) {
    if (knownIds.has(contribution.cycleId)) {
      const list = byCycle.get(contribution.cycleId) ?? []
      list.push(contribution)
      byCycle.set(contribution.cycleId, list)
    } else {
      orphans.push(contribution) // never dropped: it goes to "Unknown cycle"
    }
  }

  // Newest cycle first (highest number). A cycle with no contributions still gets a group.
  const groups: CycleGroup<C, T>[] = [...cycles]
    .sort((a, b) => b.number - a.number)
    .map((cycle) => ({ key: cycle.id, cycle, contributions: byCycle.get(cycle.id) ?? [] }))

  if (orphans.length > 0) {
    groups.push({ key: UNKNOWN_CYCLE_KEY, cycle: null, contributions: orphans })
  }
  return groups
}