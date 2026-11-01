// Pinned stokvels. Only id and name: balances, roles and members belong to the API and would go stale here.
// A store FACTORY (no module-level store): each provider makes its own.
import { createStore } from 'zustand/vanilla';
import { createJSONStorage, persist } from 'zustand/middleware';

export type Pin = { id: string; name: string };

export type PinsStore = {
  pins: Pin[];
  ownerId: string | null; // whose pins these are
  togglePin: (pin: Pin) => void;
  claim: (userId: string) => void; // different person? forget the old pins
  prune: (validIds: string[]) => void; // drop stokvels the member has left
  clear: () => void;
};

export function createPinsStore() {
  return createStore<PinsStore>()(
    persist(
      (set, get) => ({
        pins: [],
        ownerId: null,
        togglePin: (pin) =>
          set((s) =>
            s.pins.some((p) => p.id === pin.id)
              ? { pins: s.pins.filter((p) => p.id !== pin.id) }
              : { pins: [...s.pins, { id: pin.id, name: pin.name }] }, // copy ONLY id and name
          ),
        claim: (userId) => {
          if (get().ownerId !== userId) set({ pins: [], ownerId: userId });
        },
        prune: (validIds) => {
          const keep = get().pins.filter((p) => validIds.includes(p.id));
          if (keep.length !== get().pins.length) set({ pins: keep });
        },
        clear: () => set({ pins: [], ownerId: null }),
      }),
      {
        name: 'ronditrack-pins',
        storage: createJSONStorage(() => localStorage),
        skipHydration: true, // loaded AFTER the first render, so the server HTML and first client render match
        partialize: (s) => ({ pins: s.pins, ownerId: s.ownerId }),
      },
    ),
  );
}