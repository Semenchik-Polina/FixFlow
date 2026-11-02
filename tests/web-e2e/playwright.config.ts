import { defineConfig, devices } from '@playwright/test';

const ci = !!process.env.CI;

// Runs against the Compose `full` profile (README, "End-to-end tests"); nothing here starts the stack
export default defineConfig({
  testDir: './tests',
  // Every test signs up its own organization, so tests share no state
  fullyParallel: true,
  workers: 2,
  // No retries: a flaky test should fail the run, not pass on its second try
  retries: 0,
  forbidOnly: ci,
  reporter: ci ? [['github'], ['html', { open: 'never' }]] : [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: 'https://localhost:8443',
    // TLS with the exported dev certificate (plan P19), which Chromium on Linux may not trust (plan P36)
    ignoreHTTPSErrors: true,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
