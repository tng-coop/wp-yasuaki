// e2e/fixtures/wp-login.ts
import { test as base } from '@playwright/test';
import type { WPAdmin } from './wp';

type LoginCreds = { username: string; password: string };

export const test = base.extend<
  {
    loginViaPost: (creds: LoginCreds) => Promise<void>;
    loginAsAdmin: () => Promise<void>;
    wpLogout: () => Promise<void>;
  },
  { wpAdmin: WPAdmin }
>({
  loginViaPost: async ({ page, baseURL }, use) => {
    const fn = async ({ username, password }: LoginCreds) => {
      const resp = await page.request.post(`${baseURL}/wp-login.php`, {
        form: {
          log: username,
          pwd: password,
          rememberme: 'forever',
          redirect_to: `${baseURL}/wp-admin/`,
          testcookie: '1',
        },
      });
      if (!resp.ok()) throw new Error(`WP login failed: ${resp.status()} ${resp.statusText()}`);
      await page.goto('/wp-admin/', { waitUntil: 'domcontentloaded' });
    };
    await use(fn);
  },

  loginAsAdmin: async ({ loginViaPost, wpAdmin }, use) => {
    await use(async () => {
      await loginViaPost({
        username: wpAdmin.login.username,
        password: wpAdmin.login.password,
      });
    });
  },

  wpLogout: async ({ page, context }, use) => {
    const fn = async () => {
      await context.clearCookies();
      await page.addInitScript(() => localStorage.clear());
    };
    await use(fn);
  },
});
