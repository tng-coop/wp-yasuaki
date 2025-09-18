// e2e/tests/wpdi-save-draft.spec.ts
import { test, expect } from '../fixtures/test';

test.describe('WPDI Harness — save draft only (UI)', () => {
  test.setTimeout(120_000);

  test('start clean → create draft → list → verify draft row exists', async ({
    page,
    blazorURL,
    baseURL,
    loginAsAdmin,
  }) => {
    // 1) login using existing fixture (cookie auth for the app)
    await loginAsAdmin();

    // 2) open the harness in nonce mode; pass wpurl like other specs
    const url = new URL('wpdi-harness?auth=nonce', blazorURL);
    if (baseURL) url.searchParams.set('wpurl', baseURL);
    await page.goto(url.toString(), { waitUntil: 'domcontentloaded' });

    // harness shell ready
    await expect(page.getByTestId('wpdi-harness')).toBeVisible();
    const table = page.getByTestId('post-table');

    // 3) create a draft via UI (WPDI Editor.CreateAsync under the hood)
    const title = uniq('DraftOnly');
    await page.getByTestId('title-input').fill(title);
    await page.getByTestId('btn-create').click();

    // 4) list & confirm the row is present
    await page.getByTestId('btn-list').click();
    await expect(table).toBeVisible();

    // wait until the new title appears
    await expect
      .poll(async () => Number(await rowExists(table, title)), { timeout: 30_000 })
      .toBe(1);

    // 5) (optional) verify status cell shows draft
    const statusCell = () =>
      table.locator('tr', { hasText: title }).first().locator('[data-testid="cell-status"]');

    await expect
      .poll(async () => (await statusCell().innerText()).trim().toLowerCase(), { timeout: 30_000 })
      .toBe('draft');
  });
});

/* ------------------------------- helpers -------------------------------- */

function uniq(prefix = 'e2e'): string {
  return `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
}

async function rowExists(table: import('@playwright/test').Locator, title: string) {
  return (await table.locator('tr', { hasText: title }).count()) > 0;
}
