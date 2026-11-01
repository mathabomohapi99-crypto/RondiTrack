'use client'; // The store lives in the browser.

import { createContext, useContext, useEffect, useState } from 'react';
import { useStore } from 'zustand';
import { createPinsStore, type PinsStore } from '@/stores/pinsStore';

type PinsStoreApi = ReturnType<typeof createPinsStore>;

const PinsContext = createContext<PinsStoreApi | null>(null);

export function PinsProvider({ children }: { children: React.ReactNode }) {
  // One store per provider (useState keeps it for the life of the page)
  const [store] = useState(() => createPinsStore());

  // Load the saved pins AFTER the first render (skipHydration), so there are no hydration warnings
  useEffect(() => {
    void store.persist.rehydrate();
  }, [store]);

  return <PinsContext.Provider value={store}>{children}</PinsContext.Provider>;
}

export function usePinsStoreApi(): PinsStoreApi {
  const store = useContext(PinsContext);
  if (!store) throw new Error('usePinsStoreApi must be used inside <PinsProvider>');
  return store;
}

// Read a piece of the store (pass a selector)
export function usePins<T>(selector: (state: PinsStore) => T): T {
  return useStore(usePinsStoreApi(), selector);
}