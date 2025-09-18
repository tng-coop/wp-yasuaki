// e2e/tests/wpdi-harness.spec.ts
import { test, expect } from '../fixtures/test';

test.describe('WPDI Harness page (nonce path, real BlazorWASM)', () => {
  test.skip(true)
  test.setTimeout(180_000);

  test('create → list → submit → retract → publish → delete (UI-only)', async ({
    page,
    blazorURL,
    baseURL,
    loginAsAdmin,
  }) => {
    // 1) login (fixture)
    await loginAsAdmin();

    // 2) open harness in nonce mode; pass wpurl like your other specs
    const url = new URL('wpdi-harness?auth=nonce', blazorURL);
    if (baseURL) url.searchParams.set('wpurl', baseURL);
    await page.goto(url.toString(), { waitUntil: 'domcontentloaded' });

    // harness ready
    await expect(page.getByTestId('wpdi-harness')).toBeVisible();
    const table = page.getByTestId('post-table');

    // 3) create a draft via UI
    const title = uniq('HarnessPost');
    await page.getByTestId('title-input').fill(title);
    await page.getByTestId('btn-create').click();

    // 4) list & confirm row exists
    await page.getByTestId('btn-list').click();
    await expect(table).toBeVisible();
    await expect
      .poll(async () => Number(await rowExists(table, title)), { timeout: 30_000 })
      .toBe(1);

    // helper to reselect status cell (fresh each time)
    const statusCell = () =>
      table.locator('tr', { hasText: title }).first().locator('[data-testid="cell-status"]');

    // 5) status transitions (assert on the dedicated status cell)

    // Submit → pending
    await page.getByTestId('btn-submit').click();
    await page.getByTestId('btn-list').click();
    await expect
      .poll(async () => (await statusCell().innerText()).trim().toLowerCase(), { timeout: 30_000 })
      .toBe('pending');

    // Retract → draft
    await page.getByTestId('btn-retract').click();
    await page.getByTestId('btn-list').click();
    await expect
      .poll(async () => (await statusCell().innerText()).trim().toLowerCase(), { timeout: 30_000 })
      .toBe('draft');

    // Publish → publish
    await page.getByTestId('btn-publish').click();
    await page.getByTestId('btn-list').click();
    await expect
      .poll(async () => (await statusCell().innerText()).trim().toLowerCase(), { timeout: 30_000 })
      .toBe('publish');

    // 6) delete & confirm gone
    await page.getByTestId('btn-delete').click();
    await page.getByTestId('btn-list').click();
    await expect
      .poll(async () => Number(!(await table.innerText()).includes(title)), { timeout: 30_000 })
      .toBe(1);
  });
});

/* ------------------------------- helpers -------------------------------- */

function uniq(prefix = 'e2e'): string {
  return `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
}

function rowByTitle(table: import('@playwright/test').Locator, title: string) {
  return table.locator('tr', { hasText: title }).first();
}

async function rowExists(table: import('@playwright/test').Locator, title: string) {
  return (await table.locator('tr', { hasText: title }).count()) > 0;
}
