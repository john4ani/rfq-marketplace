import { expect, test } from '@playwright/test';
import { signIn } from './helpers/login';

/**
 * Golden-path smoke test — the last verification step after every Staging deploy.
 *
 * 1. Loads the deployed app at `process.env.STAGING_URL`.
 * 2. Signs in with the Staging test account.
 * 3. Asserts the authenticated shell renders and the app runs without console errors.
 *
 * Expected benign console noise (filtered out):
 *  - Chromium reports the anonymous bootstrap `POST /api/v1/auth/refresh` as a
 *    "Failed to load resource … 401" console error because no session exists yet;
 *    AuthService handles it with `catchError` — it is not an application error.
 */
const BENIGN_CONSOLE_ERRORS: RegExp[] = [
  /Failed to load resource: the server responded with a status of 401/,
  /Failed to load resource: the server responded with a status of 403/,
];

function isBenignConsoleError(text: string): boolean {
  return BENIGN_CONSOLE_ERRORS.some((pattern) => pattern.test(text));
}

test('app loads after sign-in without console errors', async ({ page }) => {
  const failures: string[] = [];
  page.on('pageerror', (error) => failures.push(`pageerror: ${error.message}`));
  page.on('console', (message) => {
    if (message.type() === 'error') failures.push(`console.error: ${message.text()}`);
  });

  // 1. Navigate to the deployed Staging app and wait for the public landing page.
  await page.goto('/');
  await expect(page.locator('h1').first()).toBeVisible({ timeout: 30_000 });

  // 2. Sign in with the test account.
  await signIn(page);

  // 3. The authenticated shell is up; give the late bootstrap calls (profiles,
  //    presence, feature flags, SEAP feed) a moment to settle before asserting.
  await expect(page.locator('.app-frame')).toBeVisible({ timeout: 30_000 });
  await page.waitForTimeout(2_500);

  const unexpected = failures.filter((failure) => !isBenignConsoleError(failure));
  expect(
    unexpected,
    `The app reported unexpected console/page errors:\n${unexpected.join('\n')}`,
  ).toEqual([]);
});
