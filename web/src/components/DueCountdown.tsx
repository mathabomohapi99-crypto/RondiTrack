// NEW: "Due today / Due tomorrow / Due in N days / Overdue by N days".
// It re-renders ONCE just after each Johannesburg midnight, not every second (it only shows days).
import { useEffect, useState } from 'react'
import { dueCountdown, msUntilNextSaMidnight } from '@/lib/countdown'

export function DueCountdown({ due, cycleStatus }: { due: string; cycleStatus: string }) {
  const [now, setNow] = useState(() => new Date())

  useEffect(() => {
    if (cycleStatus !== 'Open') return // Closed / PaidOut cycles have no countdown, so no timer
    // setTimeout (not setInterval): one timer for the next SA midnight. +50ms lands just after it.
    const id = setTimeout(() => setNow(new Date()), msUntilNextSaMidnight(now) + 50)
    return () => clearTimeout(id) // cleanup: StrictMode and unmounting must not leave a timer behind
    // The effect READS `now`, so it must run again when `now` changes: that schedules the next midnight.
  }, [now, cycleStatus])

  const text = dueCountdown(due, now, cycleStatus)
  if (text === null) return null
  return <p className="text-sm font-medium">{text}</p>
}