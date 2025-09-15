// // e2e/wp-me.nonce.spec.ts
// import { test, expect } from '../fixtures/wp';

// test.describe('Login to WP (nonce) → open Blazor /wp-me and read current user', () => {
//   test('nonce mode end-to-end via WpMe.razor', async ({ page, wpBaseUrl, blazorBaseUrl, wpAdmin }) => {
//     // 1) Login to WordPress (cookie session) with the temp admin
//     await page.goto(new URL('wp-login.php', wpBaseUrl + '/').toString());
//     await page.fill('#user_login', wpAdmin.login.username);
//     await page.fill('#user_pass', wpAdmin.login.password);
//     await page.click('#wp-submit');

//     // Be lenient about landing URL (dashboard or redirect back to login with params)
//     const esc = (s: string) => s.replace(/[-/\\^$*+?.()|[\]{}]/g, '\\$&');
//     await page.waitForURL(new RegExp(`${esc(wpBaseUrl)}/wp-(admin|login\\.php\\?)`), { timeout: 15_000 });

//     // 2) Prime Blazor app storage BEFORE it loads (WPDI endpoint)
//     await page.addInitScript(({ base }) => {
//       localStorage.setItem('wpEndpoint', base);
//       // auth mode comes from query: ?auth=nonce
//     }, { base: wpBaseUrl });

//     // 3) Go to Blazor page using nonce mode
//     const target = new URL('wp-me?auth=nonce', blazorBaseUrl + '/');
//     await page.goto(target.toString());

//     // 4) Expect the Razor page to render current user via WPDI nonce flow
//     const ok = page.getByTestId('wp-me-ok');
//     await expect(ok).toBeVisible({ timeout: 20_000 });
//     await expect(ok).toContainText('id:');
//     await expect(ok).toContainText('name:');
//   });
// });
