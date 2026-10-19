// NEW: one stokvel, seen from the signed-in user's point of view.
// The role comes ONLY from this stokvel's own props, so two cards can differ.
import type { HomeStokvelEntry } from '../api/types'
import { formatDate } from '../lib/dates'
import { formatCents } from '../lib/money'
import { isTreasurer, roleLabel } from '../lib/roles'

// The props are exactly the generated entry type (no hand-written interface)
export type StokvelCardProps = HomeStokvelEntry

export function StokvelCard({
  name,
  role,
  currentCycle,
  paidCents,
  owedCents,
  nextPayoutAt,
}: StokvelCardProps) {
  // Derived in the render, not stored in state: all paid up when nothing is still owed
  const allPaidUp = owedCents === 0

  return (
    <article className="card">
      <h3>{name}</h3>

      {/* Role as words, never colour alone */}
      <p className="role">{roleLabel(role)}</p>

      {!currentCycle ? (
        // No open cycle: a message, and no paid/owed figures
        <p>No open cycle</p>
      ) : (
        <>
          <p>
            Current cycle ({currentCycle.status}): {formatDate(currentCycle.startsAt)} to{' '}
            {formatDate(currentCycle.endsAt)}
          </p>
          <p>Paid this cycle: {formatCents(paidCents)}</p>
          {allPaidUp ? (
            <p>All paid up for this cycle</p>
          ) : (
            <p>Still owed: {formatCents(owedCents)}</p>
          )}
        </>
      )}

      {nextPayoutAt ? (
        <p>Next payout: {formatDate(nextPayoutAt)}</p>
      ) : (
        <p>No payout scheduled</p>
      )}

      {/* Only a Treasurer gets this section; a Member's card doesn't render it at all.
          Hiding is a convenience, not security. The API still returns 403 to a Member. */}
      {isTreasurer(role) && (
        <section className="treasurer">
          <h4>Treasurer jobs</h4>
          {/* Placeholders for now, wired up in Week 8 */}
          <button type="button" disabled>
            Close cycle
          </button>{' '}
          <button type="button" disabled>
            Record payout
          </button>
        </section>
      )}
    </article>
  )
}