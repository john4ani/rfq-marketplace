import { expect, type Page } from '@playwright/test';
import { requireEnv } from '../config';

/**
 * Signs the deployed app in with the configured Staging test account.
 *
 * The flow matches the current Romanian-first UI on /sign-in:
 * an e-mail and a password field, then the „Autentificare" submit button. After a
 * successful login the SPA navigates away from /sign-in to /dashboard (which the
 * marketplace guard redirects to /seap/tenders when the marketplace flag is off), so
 * the helper waits for the authenticated shell (`.app-frame`) instead of a fixed route.
 */
export async function signIn(page: Page): Promise<void> {
  const env = requireEnv();

  await page.goto('/sign-in');
  await expect(page).toHaveURL(/\/sign-in/);

  await page.locator('input[autocomplete="email"]').fill(env.email);
  await page.locator('input[autocomplete="current-password"]').fill(env.password);
  await page.getByRole('button', { name: 'Autentificare' }).click();

  // Fail fast with a clear message when the credentials are rejected.
  const errorAlert = page.locator('.error[role="alert"]');
  await errorAlert.waitFor({ state: 'visible', timeout: 5_000 }).catch(() => undefined);
  if (await errorAlert.isVisible()) {
    throw new Error(`Sign-in failed for ${env.email}: ${(await errorAlert.textContent())?.trim()}`);
  }

  await expect(page.locator('.app-frame')).toBeVisible({ timeout: 30_000 });
  await expect(page).not.toHaveURL(/\/sign-in/, { timeout: 15_000 });
}
