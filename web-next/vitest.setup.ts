import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterAll, afterEach, beforeAll } from 'vitest';
import { server } from './src/test/server';

process.env.API_BASE_URL = 'http://api.test'; // the fake API the MSW handlers answer for

// An API call nobody planned fails the test (MSW 3 name of the option)
beforeAll(() => server.listen({ onUnhandledFrame: 'error' }));
afterEach(() => {
  cleanup();
  server.resetHandlers();
});
afterAll(() => server.close());