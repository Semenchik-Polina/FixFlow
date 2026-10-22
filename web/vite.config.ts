import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

// The API's `http` launch profile. FIXFLOW_API_URL overrides it; it has no VITE_ prefix, so it never reaches the bundle.
const apiTarget = process.env['FIXFLOW_API_URL'] ?? 'http://localhost:5250';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true,
    // Same origin in development too: no CORS, and the auth cookie just works (ARCHITECTURE §16)
    proxy: {
      '/api': apiTarget,
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    css: false,
    clearMocks: true,
    restoreMocks: true,
    unstubGlobals: true,
  },
});
