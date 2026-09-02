/**
 * Central access to the Staging E2E configuration.
 *
 * Values are read from the process environment; `playwright.config.ts` already validates
 * that they are present (it fails fast when missing), so `requireEnv()` here is a second
 * safety net for anything imported directly by a test file.
 */
export const ENV = {
  stagingUrl: process.env.STAGING_URL?.trim() ?? '',
  email: process.env.E2E_TEST_EMAIL?.trim() ?? '',
  password: process.env.E2E_TEST_PASSWORD ?? '',
} as const;

export function requireEnv(): typeof ENV {
  if (!ENV.stagingUrl || !ENV.email || !ENV.password) {
    throw new Error(
      'STAGING_URL, E2E_TEST_EMAIL and E2E_TEST_PASSWORD must be set to run the staging E2E suite.',
    );
  }
  return ENV;
}
