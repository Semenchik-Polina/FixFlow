import { setupServer } from 'msw/node';

// The API as tests see it. From the first endpoints on (plan T1.3), the orval-generated MSW handlers are spread in
// here; a test overrides one with `server.use(...)`.
export const server = setupServer();
