// e2e/fixtures/wp.ts
import { test as base, expect, request } from '@playwright/test';

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
  }
>({
  // ---- Unique ID helper (worker-scoped) ----
  // ✅ worker fixture signature: ({}, use, workerInfo)
  uniq: [async ({}, use, workerInfo) => {
    const runId = String(Date.now()); // or whatever you prefer
    const wid = workerInfo.parallelIndex;
    let seq = 0;
    await use((pfx = 'e2e') => `${pfx}-${runId}-${wid}-${++seq}`);
  }, { scope: 'worker' }],

  // ---- Temp admin (worker-scoped, lazy) ----
  // ✅ worker fixture signature: ({ fixtures }, use, workerInfo)
  wpAdmin: [async ({ uniq }, use, workerInfo) => {
    const { wpBaseUrl, wpUser, wpAppPwd } = workerInfo.project.use as {
      wpBaseUrl: string;
      wpUser: string;
      wpAppPwd: string;
    };

    if (!wpBaseUrl || !wpUser || !wpAppPwd) {
      throw new Error('wpBaseUrl / wpUser / wpAppPwd must be set in project.use');
    }

    const baseUrl = wpBaseUrl.replace(/\/+$/, '');
    const token = Buffer.from(`${wpUser}:${wpAppPwd}`).toString('base64');

    const api = await request.newContext({
      baseURL: baseUrl,
      extraHTTPHeaders: { Authorization: `Basic ${token}` },
    });

    const username = uniq('admin');
    const password = 'a'; // always "a"
    const email = `${username}@e2e.local`;

    const res = await api.post('/wp-json/wp/v2/users', {
      data: { username, password, email, roles: ['administrator'] },
    });
    if (res.status() !== 201) {
      throw new Error(`Failed to create admin: ${res.status()} ${await res.text()}`);
    }
    const created = await res.json();

    await use({ id: created.id, login: { username, password } });

    // Cleanup when worker finishes
    await api.delete(`/wp-json/wp/v2/users/${created.id}?force=true&reassign=false`);
  }, { scope: 'worker' }],
});

export { expect };
