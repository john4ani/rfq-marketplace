import { defineConfig, devices } from '@playwright/test';

/**
 * Staging smoke / E2E suite — validates a freshly deployed Staging environment as the
 * last verification step after every deployment.
 *
 * Required environment variables (the config fails fast with a clear message when missing):
 *   STAGING_URL        – base URL of the deployed Staging app, e.g. http://<static-ip>:8081
 *   E2E_TEST_EMAIL     – e-mail of the Staging test account
 *   E2E_TEST_PASSWORD  – password of the Staging test account
 *
 * The SPA is Romanian-first and the browser language decides the UI strings, so the
 * Chromium context is pinned to `ro-RO` (Europe/Bucharest) to keep the selectors
 * deterministic.
 */
const stagingUrl = process.env.STAGING_URL?.trim();
const testEmail = process.env.E2E_TEST_EMAIL?.trim();
const testPassword = process.env.E2E_TEST_PASSWORD;

if (!stagingUrl) {
  throw new Error(
    'Missing required environment variable STAGING_URL.\n' +
      'Set it to the deployed Staging base URL (e.g. http://3.64.174.227:8081), then run `pnpm test:e2e:staging`.',
  );
}
if (!testEmail || !testPassword) {
  throw new Error(
    'Missing required environment variables E2E_TEST_EMAIL / E2E_TEST_PASSWORD.\n' +
      'Set the Staging test-account credentials before running the E2E suite.',
  );
}

export default defineConfig({
  testDir: './e2e',
  // The live SEAP search imports notices over the network and can take a while.
  timeout: 240_000,
  expect: { timeout: 30_000 },
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: [
    ['list'],
    ['html', { open: 'never', outputFolder: 'playwright-report' }],
  ],
  use: {
    baseURL: stagingUrl,
    locale: 'ro-RO',
    timezoneId: 'Europe/Bucharest',
    actionTimeout: 30_000,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
