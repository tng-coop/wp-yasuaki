// e2e/fixtures/index.ts (or wherever you wire fixtures)
import { test as base, request } from '@playwright/test';
import { createWpApi } from './wp-api';

export const test = base.extend<{
  wpApi: Awaited<ReturnType<typeof createWpApi>>;
}>({
  wpApi: async ({}, use, testInfo) => {
    const siteBaseURL = String(testInfo.project.use.baseURL || '');
    const api = await createWpApi(request, siteBaseURL);
    await use(api);
  },
});
export { expect } from '@playwright/test';
