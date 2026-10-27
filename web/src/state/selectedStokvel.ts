import { useSyncExternalStore } from 'react';

// The selected stokvel id lives ONLY here (not in browser storage), so sign-out can clear it
let selectedId: string | null = null;
const subscribers = new Set<() => void>();

export function setSelectedStokvelId(id: string | null) {
  selectedId = id;
  subscribers.forEach((s) => s());
}

export function clearSelectedStokvel() {
  setSelectedStokvelId(null);
}

export function useSelectedStokvelId(): string | null {
  return useSyncExternalStore(
    (callback) => {
      subscribers.add(callback);
      return () => {
        subscribers.delete(callback);
      };
    },
    () => selectedId,
  );
}
