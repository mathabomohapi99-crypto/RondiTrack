'use client'; // state + timers

import { createContext, useCallback, useContext, useMemo, useRef, useState } from 'react';

type Toast = { id: number; kind: 'success' | 'error'; message: string; correlationId?: string };
type ToastApi = { success: (message: string) => void; error: (message: string, correlationId?: string) => void };

const ToastContext = createContext<ToastApi | null>(null);

export function ToastProvider({ children }: { children: React.ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([]);
  const nextId = useRef(1);

  const dismiss = useCallback((id: number) => setToasts((all) => all.filter((t) => t.id !== id)), []);

  // success: goes away by itself after 6 seconds
  const success = useCallback(
    (message: string) => {
      const id = nextId.current++;
      setToasts((all) => [...all, { id, kind: 'success', message }]);
      setTimeout(() => dismiss(id), 6000);
    },
    [dismiss],
  );

  // error: STAYS until the person closes it, and shows the reference
  const error = useCallback((message: string, correlationId?: string) => {
    const id = nextId.current++;
    setToasts((all) => [...all, { id, kind: 'error', message, correlationId }]);
  }, []);

  const api = useMemo(() => ({ success, error }), [success, error]);

  const closeButton = (id: number) => (
    <button type="button" onClick={() => dismiss(id)} aria-label="Close notification"
      className="ml-3 min-h-6 min-w-6 rounded border px-2 text-sm">
      ×
    </button>
  );

  return (
    <ToastContext.Provider value={api}>
      {children}
      {/* Both live regions always exist, so a screen reader announces what is added to them */}
      <div className="fixed bottom-4 right-4 z-50 flex max-w-sm flex-col gap-2">
        <div role="status" aria-live="polite" className="flex flex-col gap-2">
          {toasts.filter((t) => t.kind === 'success').map((t) => (
            <div key={t.id} className="flex items-start rounded border border-green-600 bg-white p-3 text-sm text-black shadow">
              <p>{t.message}</p>
              {closeButton(t.id)}
            </div>
          ))}
        </div>
        <div role="alert" className="flex flex-col gap-2">
          {toasts.filter((t) => t.kind === 'error').map((t) => (
            <div key={t.id} className="flex items-start rounded border border-red-600 bg-white p-3 text-sm text-black shadow">
              <div>
                <p>{t.message}</p>
                {t.correlationId && <p className="mt-1 text-xs text-gray-600">Reference: {t.correlationId}</p>}
              </div>
              {closeButton(t.id)}
            </div>
          ))}
        </div>
      </div>
    </ToastContext.Provider>
  );
}

export function useToasts(): ToastApi {
  const api = useContext(ToastContext);
  if (!api) throw new Error('useToasts must be used inside <ToastProvider>');
  return api;
}