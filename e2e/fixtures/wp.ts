// e2e/fixtures/wp.ts
import { test as base, expect, type APIRequestContext } from '@playwright/test';

type WPAdmin = {
  id: number;
  login: { username: string; password: string };
};

// Important: worker-scoped fixtures go in the second generic
export const test = base.extend<
  {}, // no test-scoped fixtures here
  {
    uniq: (pfx?: string) => string;
    wpAdmin: WPAdmin;
    wpApi: APIRequestContext; // <-- provided by fixtures/common.ts
  }
>({
  // ---- Unique ID helper (worker-scoped) ----
  uniq: [async ({}, use, workerInfo) => {
    const runId = String(Date.now());
    const wid = workerInfo.parallelIndex;
    let seq = 0;
    await use((pfx = 'e2e') => `${pfx}-${runId}-${wid}-${++seq}`);
  }, { scope: 'worker' }],

  // ---- Temp admin (worker-scoped) ----
  // Uses wpApi (pre-auth’d with Basic auth) — no project.use reads here.
  wpAdmin: [async ({ uniq, wpApi }, use) => {
    const username = uniq('admin');
    const password = 'a'; // deterministic test password
    const email = `${username}@e2e.local`;

    const res = await wpApi.post('/wp-json/wp/v2/users', {
      data: { username, password, email, roles: ['administrator'] },
    });
    if (res.status() !== 201) {
      throw new Error(`Failed to create admin: ${res.status()} ${await res.text()}`);
    }
    const created = await res.json();

    await use({ id: created.id, login: { username, password } });

    // Cleanup when worker finishes
    const del = await wpApi.delete(`/wp-json/wp/v2/users/${created.id}?force=true&reassign=false`);
    expect(del.ok(), `Failed to delete temp admin: ${del.status()} ${await del.text()}`).toBeTruthy();
  }, { scope: 'worker' }],
});

export { expect };
