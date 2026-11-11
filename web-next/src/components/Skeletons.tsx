// Grey placeholder shapes shown while data streams in. Same shape as the real rows/cards.
export function RowsSkeleton({ rows = 3, lines = 2 }: { rows?: number; lines?: number }) {
  return (
    <ul aria-hidden="true" className="space-y-2">
      {Array.from({ length: rows }, (_, i) => (
        <li key={i} className="flex items-center justify-between rounded border p-3">
          <div className="space-y-1">
            <div className="h-6 w-32 animate-pulse rounded bg-gray-200" />
            {Array.from({ length: lines - 1 }, (_, j) => (
              <div key={j} className="h-4 w-20 animate-pulse rounded bg-gray-200" />
            ))}
          </div>
          <div className="h-6 w-16 animate-pulse rounded bg-gray-200" />
        </li>
      ))}
    </ul>
  );
}

export function CardGridSkeleton() {
  return (
    <div aria-hidden="true" className="grid gap-4 md:grid-cols-2">
      {[0, 1].map((i) => (
        <div key={i} className="space-y-2 rounded-lg border p-4">
          <div className="h-5 w-40 animate-pulse rounded bg-gray-200" />
          <div className="h-4 w-56 animate-pulse rounded bg-gray-200" />
          <div className="h-4 w-24 animate-pulse rounded bg-gray-200" />
        </div>
      ))}
    </div>
  );
}

export function DetailSkeleton() {
  return (
    <div aria-hidden="true" className="space-y-3">
      <div className="h-8 w-64 animate-pulse rounded bg-gray-200" />
      <div className="h-4 w-48 animate-pulse rounded bg-gray-200" />
      <div className="h-4 w-40 animate-pulse rounded bg-gray-200" />
      <div className="h-24 w-full animate-pulse rounded bg-gray-200" />
    </div>
  );
}