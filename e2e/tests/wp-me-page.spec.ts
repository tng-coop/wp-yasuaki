// tests/wp-me.ui.spec.ts
import { test, expect } from '../fixtures/common';

test.describe('Blazor Razor page /wp-me via WPDI (AppPass mode)', () => {
  test('renders current user on /wp-me', async ({ page, blazorURL, wpUser, wpAppPwd }) => {
    // 1) Prime localStorage BEFORE the app loads
    await page.addInitScript(
      ({ user, pass }) => {
        localStorage.setItem('app_user', user);
        localStorage.setItem('app_pass', pass);
      },
      { user: wpUser, pass: wpAppPwd },
    );

    // 2) Navigate to the Razor page (absolute via blazorURL fixture)
    await page.goto(new URL('wp-me?auth=apppass', blazorURL).toString());

    // 3) Expect the page to show the current user
    const ok = page.getByTestId('wp-me-ok');
    await expect(ok).toBeVisible({ timeout: 15_000 });
    await expect(ok).toContainText('id:');
    await expect(ok).toContainText('name:');
  });
});
