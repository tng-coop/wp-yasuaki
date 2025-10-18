// e2e/fixtures/wp.ts
import { test as base } from './index';
import type { createWpApi } from './wp-api';

export type WPTempUser = {
  id: number;
  login: { username: string; password: string };
};
export type WPAdmin = WPTempUser;

type Needs = { wpApi: Awaited<ReturnType<typeof createWpApi>> };
type WorkerProvides = {
  uniq: (prefix?: string) => string;
  wpAdmin: WPAdmin;
  wpEditor: WPTempUser;
  wpAuthor: WPTempUser;
};

async function verifyRole(
  wpApi: Needs['wpApi'],
  userId: number,
  role: 'administrator' | 'editor' | 'author'
) {
  const res = await wpApi.get(`users/${userId}`, { params: { context: 'edit' } });
  if (!res.ok()) {
    const body = await res.text();
    throw new Error(`[wpApi] verifyRole GET users/${userId} failed: ${res.status()} ${res.statusText()} — ${body.slice(0, 160)}`);
  }
  const data: any = await res.json();
  const roles: string[] = Array.isArray(data?.roles) ? data.roles : [];
  return roles.includes(role);
}

async function promoteToRole(
  wpApi: Needs['wpApi'],
  userId: number,
  role: 'administrator' | 'editor' | 'author'
) {
  const up = await wpApi.post(`users/${userId}`, { data: { roles: [role] } });
  if (!up.ok()) {
    const body = await up.text();
    throw new Error(`[wpApi] promoteToRole POST users/${userId} failed: ${up.status()} ${up.statusText()} — ${body.slice(0, 160)}`);
  }
}

async function createUserWithRole(
  wpApi: Needs['wpApi'],
  payload: { username: string; email: string; password: string; name: string },
  role: 'administrator' | 'editor' | 'author'
): Promise<{ id: number }> {
  // Try to set the role at creation time.
  let res = await wpApi.post('users', { data: { ...payload, roles: [role] } });

  // If the server forbids assigning roles on create, fall back to create+promote.
  if (!res.ok()) {
    const maybeJson = await res.text(); // keep for diagnostics
    let shouldFallback = false;
    try {
      const err = JSON.parse(maybeJson);
      shouldFallback =
        err?.code === 'rest_user_invalid_role' ||
        err?.code === 'rest_cannot_create_user' ||
        res.status() === 403;
    } catch {
      // Not JSON, but still may need fallback
      shouldFallback = res.status() === 403 || res.status() === 400;
    }

    if (shouldFallback) {
      // Create without roles…
      const c = await wpApi.post('users', { data: payload });
      if (!c.ok()) {
        const body = await c.text();
        throw new Error(`[wpApi] create user failed: ${c.status()} ${c.statusText()} — ${body.slice(0, 160)}`);
      }
      const created = (await c.json()) as any;

      // …then promote and verify.
      await promoteToRole(wpApi, created.id, role);
      const ok = await verifyRole(wpApi, created.id, role);
      if (!ok) throw new Error(`[wpApi] user ${created.id} was not granted role ${role}`);
      return { id: created.id };
    }

    // Not a fallback scenario—surface the original error.
    throw new Error(`[wpApi] create user with roles failed: ${res.status()} ${res.statusText()} — ${maybeJson.slice(0, 160)}`);
  }

  const created = (await res.json()) as any;

  // Double-check role; some setups ignore role on create.
  const ok = await verifyRole(wpApi, created.id, role).catch(async () => {
    // Try one promotion if GET failed due to context/auth weirdness.
    await promoteToRole(wpApi, created.id, role);
    return verifyRole(wpApi, created.id, role);
  });
  if (!ok) throw new Error(`[wpApi] user ${created.id} does not have role ${role} after creation`);
  return { id: created.id };
}

export const test = base.extend<{}, WorkerProvides & Needs>({
  uniq: [
    async ({}, use) => {
      const seed = Math.random().toString(36).slice(2, 8);
      const fn = (prefix = '') => `${prefix}${Date.now().toString(36)}_${seed}`;
      await use(fn);
    },
    { scope: 'worker' },
  ],

  wpAdmin: [
    async ({ wpApi, uniq }, use) => {
      const username = uniq('e2e_admin_');
      const password = uniq('pw_');
      const email = `${username}@example.test`;

      const { id } = await createUserWithRole(
        wpApi,
        { username, email, password, name: username },
        'administrator'
      );

      const user: WPAdmin = { id, login: { username, password } };
      await use(user);

      try {
        await wpApi.delete(`users/${id}`, { params: { reassign: 1, force: true } });
      } catch {}
    },
    { scope: 'worker' },
  ],

  wpEditor: [
    async ({ wpApi, uniq }, use) => {
      const username = uniq('e2e_editor_');
      const password = uniq('pw_');
      const email = `${username}@example.test`;

      const { id } = await createUserWithRole(
        wpApi,
        { username, email, password, name: username },
        'editor'
      );

      const user: WPTempUser = { id, login: { username, password } };
      await use(user);

      try {
        await wpApi.delete(`users/${id}`, { params: { reassign: 1, force: true } });
      } catch {}
    },
    { scope: 'worker' },
  ],

  wpAuthor: [
    async ({ wpApi, uniq }, use) => {
      const username = uniq('e2e_author_');
      const password = uniq('pw_');
      const email = `${username}@example.test`;

      const { id } = await createUserWithRole(
        wpApi,
        { username, email, password, name: username },
        'author'
      );

      const user: WPTempUser = { id, login: { username, password } };
      await use(user);

      try {
        await wpApi.delete(`users/${id}`, { params: { reassign: 1, force: true } });
      } catch {}
    },
    { scope: 'worker' },
  ],
});
