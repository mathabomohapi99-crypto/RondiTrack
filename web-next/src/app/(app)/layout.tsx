import { Suspense } from 'react';
import { ToastProvider } from '@/components/Toasts';
import { Sidebar } from './Sidebar';

// One layout for everything signed in. NEW 9.1: the toast area lives here, so a toast survives a page refresh.
export default function AppLayout({ children }: { children: React.ReactNode }) {
  return (
    <ToastProvider>
      <div className="flex min-h-screen">
        <Suspense fallback={<aside className="w-60 border-r p-4 text-sm">Loading…</aside>}>
          <Sidebar />
        </Suspense>
        <main className="flex-1 p-6">{children}</main>
      </div>
    </ToastProvider>
  );
}