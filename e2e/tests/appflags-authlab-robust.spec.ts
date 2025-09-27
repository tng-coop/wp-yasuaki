import { test, expect } from '../fixtures/test';
import type { Page } from '@playwright/test';

// Small helper to read the status text consistently
async function expectStatus(page: Page, matcher: RegExp | string, timeout = 10_000) {
  await expect(page.getByTestId('wpdi-status')).toHaveText(matcher, { timeout });
}

test.describe('AppFlags AuthLab – robustness (lean)', () => {
  test.skip('query flags respected and URL normalized', async ({ page, blazorURL, baseURL }) => {
    const url = new URL('appflags?auth=nonce', blazorURL);
    if (baseURL) url.searchParams.set('wpurl', baseURL);
    await page.goto(url.toString());

    // State panel reflects flags
    await expect(page.getByTestId('state-auth')).toHaveText('Nonce');
    if (baseURL) await expect(page.getByTestId('state-wpurl')).toHaveText(baseURL);

    // Normalizer in Program.cs should ensure these params exist in the URL
    await expect(page).toHaveURL(/auth=(nonce|App Password)/);
    await expect(page).toHaveURL(/lang=(jp|en)/);
    await expect(page).toHaveURL(/appmode=(basic|full)/);
    await expect(page).toHaveURL(/wpurl=/);
  });

  test('toggling one flag does not regress others', async ({ page, blazorURL }) => {
    await page.goto(new URL('appflags', blazorURL).toString());

    // Flip App Mode → Basic
    await page.getByTestId('appmode-basic').click();
    await expect(page.getByTestId('state-mode')).toHaveText('Basic');

    // Flip Auth → App Password (mode should remain Basic)
    await page.getByTestId('authlab-pass').click();
    await expect(page.getByTestId('state-auth')).toHaveText('App Password');
    await expect(page.getByTestId('state-mode')).toHaveText('Basic');

    // Flip Language back and forth, mode/auth should stick
    const toJapanese = page.getByTestId('lang-japanese');
    const toEnglish = page.getByTestId('lang-english');

    await toJapanese.click();
    await expect(page.getByTestId('state-lang')).toHaveText('日本語');
    await expect(page.getByTestId('state-mode')).toHaveText('Basic / 基本');
    await expect(page.getByTestId('state-auth')).toHaveText('アプリパスワード');

    await toEnglish.click();
    await expect(page.getByTestId('state-lang')).toHaveText('English');
    await expect(page.getByTestId('state-mode')).toHaveText('Basic');
    await expect(page.getByTestId('state-auth')).toHaveText('App Password');
  });

  test('App Password precedence over cookies (cookies ignored when App Password set)', async ({
    page,
    blazorURL,
    baseURL,
    loginAsAdmin,
    wpUser,
    wpAppPwd,
  }) => {
    // Seed WP cookies via admin login
    await loginAsAdmin();

    // Go to AppFlags in App Password mode with known wpurl
    const appflags = new URL('appflags?auth=App Password', blazorURL);
    if (baseURL) appflags.searchParams.set('wpurl', baseURL);
    await page.goto(appflags.toString());

    // Ensure no stored App Password → should be Unauthorized despite cookies
    await page.getByTestId('authlab-clear').click();
    await page.getByTestId('wpdi-run').click();
    await expectStatus(page, /Unauthorized|HTTP (401|403)/);

    // Store valid App Password → should become OK (because Basic overrides cookies)
    await page.getByTestId('authlab-user').fill(wpUser);
    await page.getByTestId('authlab-pass').fill(wpAppPwd);
    await page.getByTestId('authlab-save-valid').click();

    await page.getByTestId('wpdi-run').click();
    await expectStatus(page, /OK/);
    await expect(page.getByTestId('wpdi-user')).toContainText(wpUser);
  });
});
