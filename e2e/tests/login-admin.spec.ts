// e2e/tests/login-admin.spec.ts
import { test } from '../fixtures/wp-login';
import { expect } from '../fixtures';

test('admin can reach Dashboard and has admin capabilities', async ({ page, loginAsAdmin, baseURL }) => {
  await loginAsAdmin();

  // Land on Dashboard (authenticated)
  await page.goto('/wp-admin/', { waitUntil: 'domcontentloaded' });
  await expect(page.locator('#wpadminbar')).toBeVisible();
  await expect(page).toHaveURL(/\/wp-admin\/?$/);

  // --- 1) Prefer API-based verification (most robust) ---
  // Check current user's roles/capabilities using cookie-auth via page.request.
  // If /wp-json is disabled, fall back to ?rest_route=...
  let verifiedAdmin = false;
  const meEndpoints = [
    `${baseURL}/wp-json/wp/v2/users/me?context=edit`,
    `${baseURL}/?rest_route=/wp/v2/users/me&context=edit`,
  ];

  for (const url of meEndpoints) {
    const resp = await page.request.get(url);
    if (!resp.ok()) continue;

    const data: any = await resp.json().catch(() => null);
    const roles: string[] | undefined = Array.isArray(data?.roles) ? data.roles : undefined;
    const caps = data?.capabilities || {};

    // Admin if role includes 'administrator' OR has manage_options capability.
    if ((roles && roles.includes('administrator')) || caps.manage_options === true) {
      verifiedAdmin = true;
      break;
    }
  }

  // --- 2) UI fallback if REST role check isn’t available ---
  if (!verifiedAdmin) {
    // Admin-only top-level menus commonly present for administrators
    const adminMenuSelectors = ['#menu-users', '#menu-plugins', '#menu-settings', '#menu-tools'];
    const vis = await Promise.all(
      adminMenuSelectors.map((sel) => page.locator(sel).isVisible().catch(() => false)),
    );
    // Require at least two admin-only menus to be visible for stronger confidence.
    expect(vis.filter(Boolean).length).toBeGreaterThanOrEqual(2);

    // Also assert access to Settings (requires manage_options)
    const res = await page.goto('/wp-admin/options-general.php', { waitUntil: 'domcontentloaded' });
    expect(res?.ok()).toBeTruthy();
    await expect(page.locator('#wpbody-content')).toBeVisible();
    await expect(page.locator('body')).not.toContainText('Sorry, you are not allowed to access this page');
  }
});
