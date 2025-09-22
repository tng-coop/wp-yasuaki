// e2e/fixtures/common.ts
import { test as base, expect, request, type APIRequestContext } from '@playwright/test';

type TestScoped = {
};

type WorkerScoped = {
  wpUser: string;
  wpAppPwd: string;
  // ✅ This is an APIRequestContext, not APIRequest
  wpApi: APIRequestContext;
};

export const test = base.extend<TestScoped, WorkerScoped>({

  wpUser: [async ({}, use, workerInfo) => {
    const { wpUser } = workerInfo.project.use as { wpUser?: string };
    if (!wpUser) throw new Error('wpUser must be set in project.use');
    await use(wpUser);
  }, { scope: 'worker' }],

  wpAppPwd: [async ({}, use, workerInfo) => {
    const { wpAppPwd } = workerInfo.project.use as { wpAppPwd?: string };
    if (!wpAppPwd) throw new Error('wpAppPwd must be set in project.use');
    await use(wpAppPwd);
  }, { scope: 'worker' }],

  wpApi: [async ({ wpUser, wpAppPwd }, use, workerInfo) => {
    const { baseURL } = workerInfo.project.use as { baseURL?: string };
    if (!baseURL) throw new Error('baseURL must be set in project.use');

    const token = Buffer.from(`${wpUser}:${wpAppPwd}`).toString('base64');
    const api = await request.newContext({
      baseURL,
      extraHTTPHeaders: { Authorization: `Basic ${token}` },
    });

    await use(api);       // <- api is APIRequestContext
    await api.dispose();
  }, { scope: 'worker' }],
});

export { expect };
