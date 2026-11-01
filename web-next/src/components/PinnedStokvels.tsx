'use client'; // Reads the browser store.

import Link from 'next/link';
import { useEffect } from 'react';
import { usePins, usePinsStoreApi } from './PinsProvider';

// stokvelIdsKey = my stokvel ids joined by commas ('' = none, null = the sidebar could not load them)
export function PinnedStokvels({ userId, stokvelIdsKey }: { userId: string; stokvelIdsKey: string | null }) {
  const store = usePinsStoreApi();
  const pins = usePins((s) => s.pins);

  // Once the saved pins are loaded: forget them if they belong to someone else, and drop stokvels I left.
  useEffect(() => {
    const check = () => {
      store.getState().claim(userId);
      if (stokvelIdsKey !== null) store.getState().prune(stokvelIdsKey ? stokvelIdsKey.split(',') : []);
    };
    if (store.persist.hasHydrated()) {
      check();
      return;
    }
    return store.persist.onFinishHydration(check);
  }, [store, userId, stokvelIdsKey]);

  if (pins.length === 0) return null;

  return (
    <div className="mt-4">
      <p className="text-xs font-semibold uppercase text-gray-500">Pinned</p>
      <ul className="mt-1 flex flex-col gap-1">
        {pins.map((pin) => (
          <li key={pin.id}>
            <Link href={`/stokvels/${pin.id}`} className="underline">
              {pin.name}
            </Link>
          </li>
        ))}
      </ul>
    </div>
  );
}