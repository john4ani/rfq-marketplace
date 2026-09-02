import { expect, test } from '@playwright/test';
import { signIn } from './helpers/login';

/**
 * External-integration smoke test — validates that a freshly deployed Staging is really
 * connected end-to-end: the app runs the „Caută licitații (Suceava)" flow against the
 * live e-licitatie.ro (SEAP) endpoint and reads the freshly imported tenders back from
 * PostgreSQL.
 *
 * Mapping onto the current UI: the county filter is added in the advanced filters
 * (Județe → „Suceava" + „Adaugă") and the live on-demand search is triggered with the
 * „Caută în SEAP" button. Results render as tender cards (`a.card`), not an HTML table.
 *
 * Staging prerequisites:
 *  - The on-demand search must be able to reach e-licitatie.ro from the API container.
 *  - The test account should have a registered company — the county advanced-filter
 *    editor is company-gated. Without a company the test falls back to an unfiltered
 *    live search (still validates SEAP connectivity + PostgreSQL) and records a note.
 */
test('live SEAP search (Suceava) returns at least one tender', async ({ page }) => {
  await signIn(page);

  await page.goto('/seap/tenders');
  await expect(page.getByRole('heading', { name: 'Anunțuri SEAP' })).toBeVisible();

  // The filter/search section is rendered once the profile (and company) are loaded.
  await expect(page.locator('section.filters[aria-label="Filtre anunțuri"]')).toBeVisible({
    timeout: 30_000,
  });

  const countySelect = page.getByLabel('Alege județul de adăugat');
  if (await countySelect.isVisible()) {
    const countyField = page.locator('.advanced-field', { has: countySelect });
    await countySelect.selectOption('Suceava');
    await countyField.getByRole('button', { name: 'Adaugă', exact: true }).click();
    await expect(page.locator('.chip', { hasText: 'Suceava' })).toBeVisible();
  } else {
    test.info().annotations.push({
      type: 'note',
      description:
        'Advanced county editor is company-gated and the test account has no company — ran an unfiltered live SEAP search instead.',
    });
  }

  await page.getByRole('button', { name: 'Caută în SEAP' }).click();

  // The on-demand search imports notices over the network; wait for the completion
  // message (success or failure) so the assertion below is deterministic.
  const success = page.locator('.note', { hasText: 'Căutarea în SEAP s-a încheiat' });
  const failure = page.locator('.note.error', { hasText: 'Căutarea în SEAP nu a putut rula' });
  await expect(success.or(failure)).toBeVisible({ timeout: 150_000 });

  expect(
    await failure.isVisible(),
    'The live SEAP search failed (e-licitatie.ro unreachable or SEAP disabled on Staging?).',
  ).toBe(false);

  // The completion message carries the result count — require a positive count.
  const successText = await success.textContent();
  const countMatch = /Căutarea în SEAP s-a încheiat — (\d+) anunțuri potrivite\./.exec(successText ?? '');
  expect(countMatch, `Unexpected completion message: ${successText}`).not.toBeNull();
  expect(Number(countMatch![1])).toBeGreaterThan(0);

  // „Tabelul" de rezultate: the feed must render at least one tender card.
  const cards = page.locator('section.page > a.card');
  await expect(cards.first()).toBeVisible({ timeout: 30_000 });
  expect(await cards.count()).toBeGreaterThan(0);
});
