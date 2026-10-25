// NEW 7.4: remembers the Idempotency-Key for ONE intent to pay.
// Plain TypeScript (no React) so the MSW tests drive the real thing.
export function createIntentKeeper(makeKey: () => string = () => crypto.randomUUID()) {
  // The key lives inside this keeper (one per form), not in a module variable and not in the TanStack cache.
  let current: { key: string; fingerprint: string } | null = null;

  return {
    // Same fingerprint (same member + cycle + amount) = same key. Anything changed = a NEW key.
    // The key is only made when someone submits, never during render, so StrictMode's double render can't make two.
    keyFor(fingerprint: string): string {
      if (!current || current.fingerprint !== fingerprint) {
        current = { key: makeKey(), fingerprint };
      }
      return current.key;
    },
    // Forget the key: the intent finished (success) or was definitely rejected (400/403/404/409/422).
    release() {
      current = null;
    },
  };
}