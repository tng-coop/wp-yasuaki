// e2e/fixtures/wp.ts
import { test as base } from '@playwright/test';
import type { WPApi } from './common';

export type WPTempUser = {
  id: number;
  login: { username: string; password: string };
};

export type WPAdmin = WPTempUser;

// What this file NEEDS from others (worker fixtures)
type Needs = { wpApi: WPApi };

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

      const created = await wpApi.post('/wp/v2/users', {
        data: { username, email, password, name: username, role: 'administrator' },
      });

      const user: WPAdmin = { id: created.id, login: { username, password } };
      await use(user);

      try {
        await wpApi.delete(`/wp/v2/users/${created.id}`, { params: { reassign: 1, force: true } });
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

      const created = await wpApi.post('/wp/v2/users', {
        data: { username, email, password, name: username, role: 'editor' },
      });

      const user: WPTempUser = { id: created.id, login: { username, password } };
      await use(user);

      try {
        await wpApi.delete(`/wp/v2/users/${created.id}`, { params: { reassign: 1, force: true } });
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

      const created = await wpApi.post('/wp/v2/users', {
        data: { username, email, password, name: username, role: 'author' },
      });

      const user: WPTempUser = { id: created.id, login: { username, password } };
      await use(user);

      try {
        await wpApi.delete(`/wp/v2/users/${created.id}`, { params: { reassign: 1, force: true } });
      } catch {}
    },
    { scope: 'worker' },
  ],
});
