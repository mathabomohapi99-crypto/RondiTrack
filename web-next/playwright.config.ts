import { defineConfig, devices } from '@playwright/test';

const BASE_URL = 'http://localhost:3000';
for (const name of ['SESSION_SECRET', 'E2E_PASSWORD', 'API_BASE_URL']) {
  if (!process.env[name]) throw new Error(`Set ${name} first (locally: . .\\scripts\\e2e-env.ps1 from the repo root)`);
}

export default defineConfig({
  testDir: './e2e',
  workers: 1, // one at a time: the refresh token rotates, so saved sessions must be saved back in order
  retries: 0, // a flaky test must show up, not hide
  reporter: [['list'], ['html', { open: 'never' }]],
  use: { baseURL: BASE_URL, trace: 'on' }, // a trace for every test, so the journey's trace can be opened
  projects: [
    { name: 'setup', testMatch: /auth\.setup\.ts/ },
    { name: 'chromium', use: { ...devices['Desktop Chrome'] }, dependencies: ['setup'], testIgnore: /auth\.setup\.ts/ },
  ],
  // The PRODUCTION build: run `npm run build` first (the "e2e" script does)
  webServer: {
    command: 'npm run start',
    url: BASE_URL,
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
    env: { ...(process.env as Record<string, string>), PORT: '3000' },
  },
});