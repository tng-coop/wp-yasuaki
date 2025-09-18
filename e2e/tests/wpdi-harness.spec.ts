// e2e/tests/wpdi-harness.spec.ts
import { test, expect } from '../fixtures/test';

test.describe('WPDI Harness page (nonce path, real BlazorWASM)', () => {
  test.setTimeout(180_000);

  test('create → list → submit → retract → publish → delete (UI-only)', async ({
    page,
    blazorURL,
    baseURL,
    loginAsAdmin,
  }) => {
    if (!baseURL) throw new Error('baseURL required');

    // 1) login (fixture)
    await loginAsAdmin();

    // 2) open harness in nonce mode; pass wpurl like your other specs
    const url = new URL('wpdi-harness?auth=nonce', blazorURL);
    url.searchParams.set('wpurl', baseURL);
    await page.goto(url.toString(), { waitUntil: 'domcontentloaded' });

    // harness ready
    const harness = page.getByTestId('wpdi-harness');
    const table = page.getByTestId('post-table');
    await expect(harness).toBeVisible();
    await expect(table).toBeAttached(); // tbody may be hidden before listing

    // 3) create a draft via UI
    const title = uniq('HarnessPost');
    await page.getByTestId('title-input').fill(title);
    await page.getByTestId('btn-create').click();

    // 4) list & confirm row exists
    await page.getByTestId('btn-list').click();
    await expect(table).toBeVisible({ timeout: 30_000 });

    const row = () => table.locator('tr', { hasText: title }).first();
    const statusCell = () => row().locator('[data-testid="cell-status"]');

    await expect(row()).toHaveCount(1, { timeout: 30_000 });
    await expect(statusCell()).toHaveText(/draft/i, { timeout: 30_000 });

    // 5) status transitions (assert on the dedicated status cell)
    // Submit → pending
    await page.getByTestId('btn-submit').click();
    await page.getByTestId('btn-list').click();
    await expect(statusCell()).toHaveText(/pending/i, { timeout: 30_000 });

    // Retract → draft
    await page.getByTestId('btn-retract').click();
    await page.getByTestId('btn-list').click();
    await expect(statusCell()).toHaveText(/draft/i, { timeout: 30_000 });

    // Publish → publish/published
    await page.getByTestId('btn-publish').click();
    await page.getByTestId('btn-list').click();
    await expect(statusCell()).toHaveText(/publish|published/i, { timeout: 30_000 });

    // 6) delete & confirm gone (optimistic eviction happens in harness Delete())
    await page.getByTestId('btn-delete').click();

    // Status banner flips to Deleted
    await expect(page.getByTestId('status')).toHaveText(/deleted/i, { timeout: 30_000 });

    // Row should already be gone without re-listing thanks to Feed.Evict(...)
    await expect(row()).toHaveCount(0, { timeout: 30_000 });

    // Optional: force re-list and assert again (server reconciliation)
    await page.getByTestId('btn-list').click();
    await expect(row()).toHaveCount(0, { timeout: 30_000 });
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
