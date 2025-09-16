// e2e/tests/wp-nonce-me-page.spec.ts
import { test, expect } from '../fixtures/test';

test.describe('Login to WP (nonce) → open Blazor /wp-me and read current user', () => {
  test('nonce mode end-to-end via WpMe.razor', async ({ page, blazorURL, loginAsAdmin }) => {
    // 1) Login via protocol (no UI)
    await loginAsAdmin();

    // 2) Capture WP origin & prime localStorage for Blazor
    const wpOrigin = new URL(page.url()).origin; // we’re on /wp-admin now
    await page.addInitScript((origin: string) => {
      localStorage.setItem('wpEndpoint', origin);
    }, wpOrigin);

    // 3) Go straight to the Blazor page in nonce mode
    await page.goto(new URL('wp-me?auth=nonce', blazorURL).toString());

    // 4) Assertions
    const ok = page.getByTestId('wp-me-ok');
    await expect(ok).toBeVisible({ timeout: 20_000 });
    await expect(ok).toContainText('id:');
    await expect(ok).toContainText('name:');
  });
});
