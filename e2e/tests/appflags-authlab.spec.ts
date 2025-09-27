import { test, expect } from '../fixtures/test';
import type { Page } from '@playwright/test';

// Utility: wait until Quick Check status matches
async function expectStatus(page: Page, matcher: RegExp | string, timeout = 10_000) {
  await expect(page.getByTestId('wpdi-status')).toHaveText(matcher, { timeout });
}

test.describe('AppFlags Auth Scenarios Lab', () => {
  test.beforeEach(async ({ page, blazorURL }) => {
    await page.goto(new URL('appflags', blazorURL).toString());
    await expect(page.getByTestId('appflags-page')).toBeVisible();
  });

  test('no App Password set → Unauthorized in App Password mode', async ({ page }) => {
    await page.getByTestId('authlab-pass').click();
    await page.getByTestId('authlab-clear').click();
    await page.getByTestId('wpdi-run').click();

    await expectStatus(page, /Unauthorized/);
    await expect(page.getByTestId('authlab-creds-present')).toHaveText('No');
  });

  test('invalid App Password saved → Unauthorized', async ({ page, wpUser }) => {
    await page.getByTestId('authlab-pass').click();
    await page.getByTestId('authlab-user').fill(wpUser);
    await page.getByTestId('authlab-pass').fill('DefinitelyWrongPassword');
    await page.getByTestId('authlab-save-invalid').click();

    await page.getByTestId('wpdi-run').click();
    await expectStatus(page, /Unauthorized/);
    await expect(page.getByTestId('authlab-creds-present')).toHaveText('yes');
    await expect(page.getByTestId('authlab-status')).toContainText('invalid');
  });

  test('valid App Password saved → Quick Check OK', async ({ page, wpUser, wpAppPwd }) => {
    await page.getByTestId('authlab-pass').click();
    await page.getByTestId('authlab-user').fill(wpUser);
    await page.getByTestId('authlab-pass').fill(wpAppPwd);
    await page.getByTestId('authlab-save-valid').click();

    await page.getByTestId('wpdi-run').click();
    await expectStatus(page, /OK/);
    await expect(page.getByTestId('wpdi-user')).toContainText(wpUser);
  });

  test('nonce not ready → Unauthorized, then OK after login', async ({ page, blazorURL, baseURL, loginAsAdmin }) => {
    // 1) Start on the app in nonce mode (and set wpurl)
    const appflags = new URL('appflags?auth=nonce', blazorURL);
    if (baseURL) appflags.searchParams.set('wpurl', baseURL);
    await page.goto(appflags.toString());

    // 2) Before login, should be Unauthorized
    await page.getByTestId('wpdi-run').click();
    await expect(page.getByTestId('wpdi-status')).toHaveText(/Unauthorized|HTTP 401|HTTP 403/);

    // 3) Login on the WP origin (this navigates to baseURL/wp-admin)
    await loginAsAdmin();

    // 4) Come back to the app and re-run
    await page.goto(appflags.toString());
    await page.getByTestId('wpdi-run').click();
    await expect(page.getByTestId('wpdi-status')).toHaveText(/OK/);
    await expect(page.getByTestId('wpdi-user')).toContainText(/.+/);
  });

  test('force rebuild client re-applies endpoint and auth', async ({ page, wpUser, wpAppPwd }) => {
    await page.getByTestId('authlab-pass').click();
    await page.getByTestId('authlab-user').fill(wpUser);
    await page.getByTestId('authlab-pass').fill(wpAppPwd);
    await page.getByTestId('authlab-save-valid').click();

    await page.getByTestId('authlab-rebuild').click();
    await expectStatus(page, /OK/);
  });
});
