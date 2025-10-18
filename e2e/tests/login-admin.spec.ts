// e2e/tests/login-admin.spec.ts
import { test } from '../fixtures/wp-login';
import { expect } from '@playwright/test';

test('admin can reach Dashboard', async ({ page, loginAsAdmin }) => {
  await loginAsAdmin();
  await page.goto('/wp-admin/');
  // Admin bar is only shown when authenticated.
  await expect(page.locator('#wpadminbar')).toBeVisible();
  await expect(page).toHaveURL(/\/wp-admin\/?$/);
});
