import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

// Separate config for tests so the dev build config stays untouched
export default defineConfig({
  plugins: [react()],
  test: { environment: 'jsdom' },
});