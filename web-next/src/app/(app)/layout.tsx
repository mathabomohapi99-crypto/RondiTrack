import { Suspense } from 'react';
import { Sidebar } from './Sidebar';

// One layout for everything signed in. The session is read inside <Suspense>, like the demo.
export default function AppLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex min-h-screen">
      <Suspense fallback={<aside className="w-60 border-r p-4 text-sm">Loading…</aside>}>
        <Sidebar />
      </Suspense>
      <main className="flex-1 p-6">{children}</main>
    </div>
  );
}