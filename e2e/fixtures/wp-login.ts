import { test as base } from './wp';
import type { WPAdmin, WPTempUser } from './wp'; // <-- add this

type LoginCreds = { username: string; password: string };

type Provides = {
  loginViaPost: (creds: LoginCreds) => Promise<void>;
  loginAsAdmin: () => Promise<void>;
  loginAsEditor: () => Promise<void>;
  loginAsAuthor: () => Promise<void>;
  wpLogout: () => Promise<void>;
};

type Needs = {
  wpAdmin: WPAdmin;
  wpEditor: WPTempUser;
  wpAuthor: WPTempUser;
};

export const test = base.extend<Provides, Needs>({
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
      await loginViaPost({ username: wpAdmin.login.username, password: wpAdmin.login.password });
    });
  },

  loginAsEditor: async ({ loginViaPost, wpEditor }, use) => {
    await use(async () => {
      await loginViaPost({ username: wpEditor.login.username, password: wpEditor.login.password });
    });
  },

  loginAsAuthor: async ({ loginViaPost, wpAuthor }, use) => {
    await use(async () => {
      await loginViaPost({ username: wpAuthor.login.username, password: wpAuthor.login.password });
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
