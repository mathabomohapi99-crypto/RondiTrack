import { useContributions, useCycles, useStokvel } from '../api/queries';
import { formatCentsAsRand } from '../lib/rand';
import { ErrorView } from './ErrorView';
import { Skeleton } from './Skeleton';

// NOTE: do not give this component key={stokvelId}. Remounting would hide the keepPreviousData
// bug instead of fixing it, and the Part 4 proof test would no longer prove anything.
export function StokvelScreen({ stokvelId, onBack }: { stokvelId: string; onBack: () => void }) {
  const stokvel = useStokvel(stokvelId);
  const cycles = useCycles(stokvelId);

  // Show the open cycle, otherwise the newest one
  const sorted = cycles.data ? [...cycles.data].sort((a, b) => b.number - a.number) : [];
  const cycle = sorted.find((c) => c.status === 'Open') ?? sorted[0];

  const contributions = useContributions(stokvelId, cycle?.id);

  // 1) error state is checked FIRST so a 404 never leaves a spinner forever
  if (stokvel.isError || cycles.isError || contributions.isError) {
    const error = stokvel.error ?? cycles.error ?? contributions.error;
    return (
      <ErrorView
        error={error}
        onBack={onBack}
        onRetry={() => {
          void stokvel.refetch();
          void cycles.refetch();
          void contributions.refetch();
        }}
      />
    );
  }

  // 2) loading state: skeleton, never the old stokvel's rows
  if (stokvel.isPending || cycles.isPending) return <Skeleton label="Loading stokvel" />;

  // 3) empty state: no cycle yet
  if (!cycle) {
    return (
      <section>
        <h2>{stokvel.data.name}</h2>
        <p>
          No contribution cycle has been started yet. This is normal for a new stokvel. The
          treasurer can start the first cycle.
        </p>
      </section>
    );
  }

  if (contributions.isPending) return <Skeleton label="Loading contributions" />;

  const rows = contributions.data.pages.flatMap((page) => page.items);
  const totalCents = rows.reduce((sum, c) => sum + c.amountCents, 0);

  return (
    <section>
      <h2>{stokvel.data.name}</h2>
      <p>Cycle {cycle.number}</p>

      {/* 3) empty state: cycle exists but nobody has paid yet */}
      {rows.length === 0 ? (
        <p>
          No contributions yet. This is normal for a new cycle. The treasurer can record the
          first contribution once a member pays in.
        </p>
      ) : (
        // 4) data state
        <>
          <p>Total shown: <strong>{formatCentsAsRand(totalCents)}</strong></p>
          <ul>
            {rows.map((c) => (
              <li key={c.id}>
                {c.memberName}: {formatCentsAsRand(c.amountCents)} ({c.status})
              </li>
            ))}
          </ul>
          {contributions.hasNextPage && (
            <button
              onClick={() => void contributions.fetchNextPage()}
              disabled={contributions.isFetchingNextPage}
            >
              {contributions.isFetchingNextPage ? 'Loading…' : 'Load more'}
            </button>
          )}
        </>
      )}
    </section>
  );
}