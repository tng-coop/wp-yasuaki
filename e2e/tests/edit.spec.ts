import { test, expect } from '../fixtures/test';

async function gotoEditNonce(page: any, blazorURL: string, baseURL?: string) {
  const wpOrigin = new URL(baseURL ?? page.url()).origin;
  await page.addInitScript(
    (origin: string) => localStorage.setItem('wpEndpoint', origin),
    wpOrigin
  );

  const url = new URL('edit?auth=nonce', blazorURL);
  if (baseURL) url.searchParams.set('wpurl', baseURL);
  await page.goto(url.toString());
  await expect(page.getByTestId('edit-page')).toBeVisible();
}

test.describe('Edit.razor — real WordPress, unique data, no skips', () => {
  test.beforeEach(async ({ loginAsAdmin }) => {
    await loginAsAdmin();
  });

  test('search seeded draft → open → autosave → save draft', async ({ page, blazorURL, baseURL, seedDraft }) => {
    await gotoEditNonce(page, blazorURL, baseURL);

    await page.getByTestId('search-input').fill(seedDraft.title);
    await page.getByTestId('search-button').click();
    await page.getByTestId(`post-open-${seedDraft.id}`).click();

    await expect(page.getByTestId('title-input')).toBeVisible();
    await page.getByTestId('title-input').fill(`${seedDraft.title} (edited)`);
    await page.getByTestId('body-input').fill('<p>Edited content via e2e</p>');

    await page.getByTestId('autosave-button').click();
    await expect(page.getByTestId('toast')).toHaveText(/Autosaved|Saved/i, { timeout: 20_000 });

    await page.getByTestId('save-draft-button').click();
    await expect(page.getByTestId('toast')).toContainText(/draft/i, { timeout: 20_000 });
  });

  test('create & publish via UI with uniq title → shows in list as Published', async ({ page, blazorURL, baseURL, uniq }) => {
    await gotoEditNonce(page, blazorURL, baseURL);

    const title = uniq('ui-publish');
    await page.getByTestId('new-article').click();
    await page.getByTestId('title-input').fill(title);
    await page.getByTestId('body-input').fill('<p>Published content via UI</p>');

    await page.getByTestId('publish-button').click();
    await expect(page.getByTestId('toast')).toContainText(/publish/i, { timeout: 20_000 });

    await page.getByTestId('search-input').fill(title);
    await page.getByTestId('search-button').click();
    await expect(page.getByTestId('posts-list')).toBeVisible();
    await page.getByTestId(`post-open-${title}`).click(); // or refine selector if you emit per-id testid
    await expect(page.getByTestId('status-badge')).toHaveText(/publish/i);
  });

  test('lock → unlock on seeded draft', async ({ page, blazorURL, baseURL, seedDraft }) => {
    await gotoEditNonce(page, blazorURL, baseURL);

    await page.getByTestId('search-input').fill(seedDraft.title);
    await page.getByTestId('search-button').click();
    await page.getByTestId(`post-open-${seedDraft.id}`).click();

    await page.getByTestId('lock-button').click();
    await expect(page.getByTestId('toast')).toBeVisible();

    await page.getByTestId('unlock-button').click();
    await expect(page.getByTestId('toast')).toBeVisible();
  });

  test('status filter: Published shows our uniquely-seeded published post', async ({ page, blazorURL, baseURL, seedPublish }) => {
    await gotoEditNonce(page, blazorURL, baseURL);

    const pub = await seedPublish('pub-list');
    await page.getByTestId('status-filter').selectOption('publish');

    await page.getByTestId('search-input').fill(pub.title);
    await page.getByTestId('search-button').click();

    await page.getByTestId(`post-open-${pub.id}`).click();
    await expect(page.getByTestId('status-badge')).toHaveText(/publish/i);
  });
});
