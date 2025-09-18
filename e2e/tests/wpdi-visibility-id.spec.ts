import { test, expect } from '../fixtures/test';

test.describe('WPDI Harness — draft → publish → delete', () => {
  test.setTimeout(180_000);

  test('row disappears after delete', async ({
    page,
    blazorURL,
    baseURL,
    loginAsAdmin,
  }) => {
    if (!baseURL) throw new Error('baseURL required');

    const uniq = (p = 'VisibilityId') =>
      `${p}-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
    const title = uniq();

    await loginAsAdmin();
    const url = new URL('wpdi-harness?auth=nonce', blazorURL);
    url.searchParams.set('wpurl', baseURL);
    await page.goto(url.toString(), { waitUntil: 'domcontentloaded' });

    const harness = page.getByTestId('wpdi-harness');
    const table = page.getByTestId('post-table');

    await expect(harness).toBeVisible();
    await expect(table).toBeAttached(); // may be hidden before listing

    // create draft
    await page.getByTestId('title-input').fill(title);
    await page.getByTestId('btn-create').click();

    // list & wait for the row
    await page.getByTestId('btn-list').click();
    await expect(table).toBeVisible({ timeout: 30_000 });

    const row = () => table.locator('tr', { hasText: title }).first();
    await expect(row()).toHaveCount(1, { timeout: 30_000 });
    await expect(row().locator('[data-testid="cell-status"]')).toHaveText(/draft/i, { timeout: 30_000 });

    // publish
    await page.getByTestId('btn-publish').click();
    await page.getByTestId('btn-list').click();
    await expect(row().locator('[data-testid="cell-status"]')).toHaveText(/publish|published/i, { timeout: 30_000 });

    // delete via WPDI (optimistic eviction happens in harness Delete())
    await page.getByTestId('btn-delete').click();

    // status banner flips to Deleted
    await expect(page.getByTestId('status')).toHaveText(/Deleted/i, { timeout: 30_000 });

    // with optimistic Evict, the row should already be gone without re-listing
    await expect(row()).toHaveCount(0, { timeout: 30_000 });

    // optional: force a re-list and assert again (server reconciliation)
    await page.getByTestId('btn-list').click();
    await expect(row()).toHaveCount(0, { timeout: 30_000 });
  });
});
