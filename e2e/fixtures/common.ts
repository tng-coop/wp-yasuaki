// e2e/fixtures/common.ts
import { test as base, request as pwRequest, type APIRequestContext } from '@playwright/test';

export type WPApi = {
  get: (path: string, opts?: { params?: Record<string, unknown> }) => Promise<any>;
  post: (path: string, opts?: { data?: unknown; params?: Record<string, unknown> }) => Promise<any>;
  delete: (path: string, opts?: { params?: Record<string, unknown> }) => Promise<any>;
};

type WorkerFixtures = {
  wpApi: WPApi;
};

export const test = base.extend<{}, WorkerFixtures>({
  wpApi: [
    // ⬇️ 3rd param is workerInfo; use it to read project.use.baseURL
    async ({}, use, workerInfo) => {
      const baseURL = workerInfo.project.use.baseURL as string | undefined;

      // Create a worker-scoped APIRequestContext that doesn't rely on test fixtures
      const ctx: APIRequestContext = await pwRequest.newContext({
        baseURL, // ok if undefined; we’ll still accept absolute paths below
      });

      const api: WPApi = {
        get: (path, opts) => ctx.get(path, opts as any).then(r => r.json()),
        post: (path, opts) => ctx.post(path, opts as any).then(r => r.json()),
        delete: (path, opts) => ctx.delete(path, opts as any).then(r => r.json()),
      };

      try {
        await use(api);
      } finally {
        await ctx.dispose();
      }
    },
    { scope: 'worker' },
  ],
});
