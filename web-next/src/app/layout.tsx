import type { Metadata } from 'next';
import { Suspense } from 'react';
import { NuqsAdapter } from 'nuqs/adapters/next/app';
import { PinsProvider } from '@/components/PinsProvider';
import { QueryProvider } from '@/components/QueryProvider';
import './globals.css';

export const metadata: Metadata = { title: 'RondiTrack' };

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body>
        {/* nuqs reads the URL with useSearchParams, which needs a Suspense boundary above it */}
        <Suspense fallback={null}>
          <NuqsAdapter>
            <QueryProvider>
              <PinsProvider>{children}</PinsProvider>
            </QueryProvider>
          </NuqsAdapter>
        </Suspense>
      </body>
    </html>
  );
}