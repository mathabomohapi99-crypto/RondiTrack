'use client'; // focus() needs the browser

import { useEffect, useRef } from 'react';

// Each step's heading takes focus, so a screen reader reads "Step 2 of 3: ..." when the step appears.
// The parent gives it key={screen}, so it re-focuses on every step.
export function FocusHeading({ children }: { children: React.ReactNode }) {
  const ref = useRef<HTMLHeadingElement>(null);
  useEffect(() => {
    ref.current?.focus();
  }, []);
  return (
    <h1 ref={ref} tabIndex={-1} className="text-2xl font-bold focus:outline focus:outline-2 focus:outline-offset-4">
      {children}
    </h1>
  );
}