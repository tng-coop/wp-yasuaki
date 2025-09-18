// e2e/tests/wpdi-visibility-id.spec.ts
import { test, expect } from '../fixtures/test';

test.describe('WPDI Harness — public visibility by ID (UI-only)', () => {
  test.setTimeout(180_000);

  test('draft is NOT visible to visitors; publishing makes it visible', async ({
    page,
    blazorURL,
    baseURL,
    loginAsAdmin,
    browser,
  }) => {
    if (!baseURL) {
      throw new Error('baseURL (WordPress origin) is required for public visibility checks.');
    }

    // 1) Admin login for harness operations
    await loginAsAdmin();

    // 2) Open harness
    const url = new URL('wpdi-harness?auth=nonce', blazorURL);
    url.searchParams.set('wpurl', baseURL);
    await page.goto(url.toString(), { waitUntil: 'domcontentloaded' });

    await expect(page.getByTestId('wpdi-harness')).toBeVisible();
    const table = page.getByTestId('post-table');

    // 3) Create draft
    const title = uniq('VisibilityId');
    await page.getByTestId('title-input').fill(title);
    await page.getByTestId('btn-create').click();

    // 4) List & capture post ID
    await page.getByTestId('btn-list').click();
    const row = () => table.locator('tr', { hasText: title }).first();

    await expect
      .poll(async () => Number((await row().count()) > 0), { timeout: 30_000 })
      .toBe(1);

    const idText = (await row().locator('[data-testid="cell-id"]').innerText()).trim();
    const postId = parseInt(idText, 10);
    expect(postId).toBeGreaterThan(0);

    await expect
      .poll(async () => (await row().locator('[data-testid="cell-status"]').innerText()).trim().toLowerCase(),
            { timeout: 30_000 })
      .toBe('draft');

    // Build public URL by ID
    const publicUrl = new URL('/', baseURL);
    publicUrl.searchParams.set('p', String(postId));

    // 5) Visitor context: draft should NOT be visible
    const anonCtx = await browser.newContext();
    const anonPage = await anonCtx.newPage();

    await anonPage.goto(publicUrl.toString(), { waitUntil: 'domcontentloaded' });
    await expect
      .poll(async () => {
        const body = (await anonPage.content()).toLowerCase();
        return Number(!body.includes(title.toLowerCase()));
      }, { timeout: 20_000, message: 'Draft title should NOT be visible to visitors' })
      .toBe(1);

    // 6) Publish in admin harness
    await page.getByTestId('btn-publish').click();
    await page.getByTestId('btn-list').click();

    await expect
      .poll(async () => {
        const txt = (await row().locator('[data-testid="cell-status"]').innerText()).trim().toLowerCase();
        return Number(txt === 'publish' || txt === 'published' || txt.includes('公開'));
      }, { timeout: 30_000, message: 'Row status should reflect published state' })
      .toBe(1);

    // 7) Visitor reload: published post should now be visible
    await anonPage.reload({ waitUntil: 'domcontentloaded' });
    await expect
      .poll(async () => {
        const body = (await anonPage.content()).toLowerCase();
        return Number(body.includes(title.toLowerCase()));
      }, { timeout: 30_000, message: 'Published post title should be visible to visitors' })
      .toBe(1);

    // 8) Cleanup
    await page.getByTestId('btn-delete').click();
    await page.getByTestId('btn-list').click();
    await expect
      .poll(async () => Number(!(await table.innerText()).includes(title)), { timeout: 30_000 })
      .toBe(1);

    await anonCtx.close();
  });
});

/* ------------------------------- helpers -------------------------------- */

function uniq(prefix = 'e2e'): string {
  return `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
}
