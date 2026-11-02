import { setupServer } from 'msw/node';

// The API as tests see it: no handler by default, so a request a test didn't expect fails it. A test answers the calls
// it makes with `server.use(...)`: the orval-generated handlers with explicit data (`getGetMeMockHandler(me)`), or the
// helpers in fixtures.ts for errors.
export const server = setupServer();
