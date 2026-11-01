'use client'; // localStorage is only reachable from the browser.

import { useEffect } from 'react';
import { usePinsStoreApi } from './PinsProvider';

// Shown on /login and /register. Arriving here means nobody is signed in, whether the person signed out,
// the session expired or it was refused. So the saved pins (personal information) are wiped from this browser.
export function ClearPinsOnLogin() {
  const store = usePinsStoreApi();
  useEffect(() => {
    store.getState().clear();
    store.persist.clearStorage();
  }, [store]);
  return null;
}