import { setupServer } from 'msw/node';

// Tests add their own handlers with server.use(...)
export const server = setupServer();