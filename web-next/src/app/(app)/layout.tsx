import { Suspense } from 'react';
import { ToastProvider } from '@/components/Toasts';
import { Sidebar } from './Sidebar';
import type { Metadata } from 'next';

// Every signed-in page is noindex, set ONCE here. robots.txt must NOT Disallow them: a blocked page never shows its noindex.
export const metadata: Metadata = { robots: { index: false, follow: false } };

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

export const metadata: Metadata = {
  metadataBase: new URL(process.env.SITE_URL ?? 'http://localhost:3000'),
  title: { default: 'RondiTrack', template: '%s | RondiTrack' },
  description: 'RondiTrack helps stokvel groups record contributions, track arrears and pay out on time.',
  openGraph: {
    siteName: 'RondiTrack', type: 'website', title: 'RondiTrack',
    description: 'Stokvel contributions, arrears and payouts in one place.',
  },
};
  );
}