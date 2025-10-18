// e2e/fixtures/wp.ts
import { test as base } from './index';
import type { createWpApi } from './wp-api';

export type WPTempUser = {
  id: number;
  login: { username: string; password: string };
};

export type WPAdmin = WPTempUser;

// What this file NEEDS from others (worker fixtures)
type Needs = { wpApi: Awaited<ReturnType<typeof createWpApi>> };

// What this file PROVIDES (all worker-scoped)
type WorkerProvides = {
  uniq: (prefix?: string) => string;
  wpAdmin: WPAdmin;
  wpEditor: WPTempUser;
  wpAuthor: WPTempUser;
};

// ⬇️ Put worker fixtures in the SECOND generic parameter
export const test = base.extend<{}, WorkerProvides & Needs>({
  // --- uniq (worker)
  uniq: [
    async ({}, use) => {
      const seed = Math.random().toString(36).slice(2, 8);
      const fn = (prefix = '') => `${prefix}${Date.now().toString(36)}_${seed}`;
      await use(fn);
    },
    { scope: 'worker' },
  ],

  // --- admin temp user (worker)
  wpAdmin: [
    async ({ wpApi, uniq }, use) => {
      const username = uniq('e2e_admin_');
      const password = uniq('pw_');
      const email = `${username}@example.test`;

      const res = await wpApi.post('users', {
        data: { username, email, password, name: username, role: 'administrator' },
      });
      if (!res.ok()) {
        const body = await res.text();
        throw new Error(
          `[wpApi] create admin failed: ${res.status()} ${res.statusText()} — ${body.slice(0, 160)}`
        );
      }
      const created: any = await res.json();

      const user: WPAdmin = { id: created.id, login: { username, password } };
      await use(user);

      try {
        await wpApi.delete(`users/${created.id}`, { params: { reassign: 1, force: true } });
      } catch {}
    },
    { scope: 'worker' },
  ],

  // --- editor temp user (worker)
  wpEditor: [
    async ({ wpApi, uniq }, use) => {
      const username = uniq('e2e_editor_');
      const password = uniq('pw_');
      const email = `${username}@example.test`;

      const res = await wpApi.post('users', {
        data: { username, email, password, name: username, role: 'editor' },
      });
      if (!res.ok()) {
        const body = await res.text();
        throw new Error(
          `[wpApi] create editor failed: ${res.status()} ${res.statusText()} — ${body.slice(0, 160)}`
        );
      }
      const created: any = await res.json();

      const user: WPTempUser = { id: created.id, login: { username, password } };
      await use(user);

      try {
        await wpApi.delete(`users/${created.id}`, { params: { reassign: 1, force: true } });
      } catch {}
    },
    { scope: 'worker' },
  ],

  // --- author temp user (worker)
  wpAuthor: [
    async ({ wpApi, uniq }, use) => {
      const username = uniq('e2e_author_');
      const password = uniq('pw_');
      const email = `${username}@example.test`;

      const res = await wpApi.post('users', {
        data: { username, email, password, name: username, role: 'author' },
      });
      if (!res.ok()) {
        const body = await res.text();
        throw new Error(
          `[wpApi] create author failed: ${res.status()} ${res.statusText()} — ${body.slice(0, 160)}`
        );
      }
      const created: any = await res.json();

      const user: WPTempUser = { id: created.id, login: { username, password } };
      await use(user);

      try {
        await wpApi.delete(`users/${created.id}`, { params: { reassign: 1, force: true } });
      } catch {}
    },
    { scope: 'worker' },
  ],
});
