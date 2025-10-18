// e2e/fixtures/index.ts
import { test as base, expect, request as pwRequest } from '@playwright/test';
import { createWpApi } from './wp-api';

type WorkerProvides = {
  wpApi: Awaited<ReturnType<typeof createWpApi>>;
};

export const test = base.extend<{}, WorkerProvides>({
  wpApi: [
    async ({}, use) => {
      const baseURL = process.env.WP_BASE_URL;
      const user = process.env.WP_USERNAME;
      const appPass = process.env.WP_APP_PASSWORD?.replace(/\s+/g, '');

      if (!baseURL) throw new Error('WP_BASE_URL is not set');
      if (!user || !appPass) throw new Error('WP_USERNAME / WP_APP_PASSWORD are required');

      const auth = Buffer.from(`${user}:${appPass}`).toString('base64');

      // Create a request context at WORKER scope (no dependency on test fixtures).
      const context = await pwRequest.newContext({
        baseURL,
        extraHTTPHeaders: {
          Authorization: `Basic ${auth}`,
          Accept: 'application/json',
          'Content-Type': 'application/json',
        },
      });

      const api = await createWpApi(context);
      await use(api);
      await context.dispose();
    },
    { scope: 'worker' },
  ],
});

export { expect };
