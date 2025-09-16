// e2e/tests/wp-nonce-me-page.spec.ts
import { test, expect } from '../fixtures/test';

test.describe('Login to WP (nonce) → open Blazor /wp-me and read current user', () => {
  test('nonce mode end-to-end via WpMe.razor', async ({ page, blazorURL, wpAdmin }) => {
    await page.goto('/wp-login.php');
    await page.fill('#user_login', wpAdmin.login.username);
    await page.fill('#user_pass', wpAdmin.login.password);
    await page.click('#wp-submit');

    await page.waitForURL(/\/wp-(admin|login\.php\?)/, { timeout: 15_000 });

    await page.addInitScript(() => {
      localStorage.setItem('wpEndpoint', window.location.origin);
    });

    await page.goto(new URL('wp-me?auth=nonce', blazorURL).toString());

    const ok = page.getByTestId('wp-me-ok');
    await expect(ok).toBeVisible({ timeout: 20_000 });
    await expect(ok).toContainText('id:');
    await expect(ok).toContainText('name:');
  });
});
