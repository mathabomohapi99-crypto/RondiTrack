// NEW: tests for the grouping function.
import { describe, expect, it } from 'vitest'
import { UNKNOWN_CYCLE_KEY, groupContributionsByCycle } from './groupContributionsByCycle'

const cycles = [
  { id: 'c1', number: 1 },
  { id: 'c3', number: 3 },
  { id: 'c2', number: 2 },
]

const contributions = [
  { id: 'a', cycleId: 'c3' },
  { id: 'b', cycleId: 'c1' },
  { id: 'c', cycleId: 'c3' },
]

describe('groupContributionsByCycle', () => {
  it('puts the newest cycle first', () => {
    const groups = groupContributionsByCycle(cycles, contributions)
    expect(groups.map((g) => g.key)).toEqual(['c3', 'c2', 'c1'])
  })

  it('still shows a cycle that has no contributions', () => {
    const groups = groupContributionsByCycle(cycles, contributions)
    const empty = groups.find((g) => g.key === 'c2')
    expect(empty?.contributions).toEqual([])
  })

  it('keeps contributions in the order given, under the right cycle', () => {
    const groups = groupContributionsByCycle(cycles, contributions)
    expect(groups[0].contributions.map((c) => c.id)).toEqual(['a', 'c'])
    expect(groups[2].contributions.map((c) => c.id)).toEqual(['b'])
  })

  it('puts an unknown cycle id in a final Unknown cycle group and never drops it', () => {
    const withOrphan = [...contributions, { id: 'z', cycleId: 'missing' }]
    const groups = groupContributionsByCycle(cycles, withOrphan)
    const last = groups[groups.length - 1]
    expect(last.key).toBe(UNKNOWN_CYCLE_KEY)
    expect(last.cycle).toBeNull()
    expect(last.contributions.map((c) => c.id)).toEqual(['z'])
  })

  it('has no Unknown cycle group when nothing is orphaned', () => {
    const groups = groupContributionsByCycle(cycles, contributions)
    expect(groups.some((g) => g.key === UNKNOWN_CYCLE_KEY)).toBe(false)
  })

  it('the total number of contributions out equals the number in', () => {
    const withOrphan = [...contributions, { id: 'z', cycleId: 'missing' }]
    const groups = groupContributionsByCycle(cycles, withOrphan)
    const total = groups.reduce((sum, g) => sum + g.contributions.length, 0)
    expect(total).toBe(withOrphan.length)
  })
})