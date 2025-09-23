// e2e/fixtures/wp-content.ts
import { test as base, expect, type APIRequestContext } from '@playwright/test';

type TestScoped = {
  seedCategory: { id: number; name: string };
  seedDraft: { id: number; title: string };
  seedPublish: (titlePrefix?: string) => Promise<{ id: number; title: string }>;
};

type WorkerScoped = {
  uniq: (pfx?: string) => string;     // from fixtures/wp.ts
  wpApi: APIRequestContext;           // from fixtures/common.ts (Basic auth against WP REST)
};

export const test = base.extend<TestScoped, WorkerScoped>({
  // Unique category per test (deleted after)
  seedCategory: [async ({ wpApi, uniq }, use) => {
    const name = uniq('cat');
    const res = await wpApi.post('/wp-json/wp/v2/categories', { data: { name } });
    if (!res.ok()) throw new Error(`Failed to create category: ${res.status()} ${await res.text()}`);
    const cat = await res.json();
    await use({ id: cat.id as number, name: cat.name as string });

    const del = await wpApi.delete(`/wp-json/wp/v2/categories/${cat.id}?force=true`);
    if (!del.ok()) console.warn(`WARN: failed to delete category ${cat.id}: ${del.status()} ${await del.text()}`);
  }, { scope: 'test' }],

  // Unique draft per test (deleted after)
  seedDraft: [async ({ wpApi, seedCategory, uniq }, use) => {
    const title = uniq('draft');
    const res = await wpApi.post('/wp-json/wp/v2/posts', {
      data: { title, status: 'draft', content: '<p>seed draft</p>', categories: [seedCategory.id] }
    });
    if (!res.ok()) throw new Error(`Failed to create draft: ${res.status()} ${await res.text()}`);
    const body = await res.json();
    const draft = { id: body.id as number, title: body.title.rendered as string };
    await use(draft);

    const del = await wpApi.delete(`/wp-json/wp/v2/posts/${draft.id}?force=true`);
    if (!del.ok()) console.warn(`WARN: failed to delete draft ${draft.id}: ${del.status()} ${await del.text()}`);
  }, { scope: 'test' }],

  // Factory to create unique published posts (each call cleaned up after the test)
  seedPublish: [async ({ wpApi, seedCategory, uniq }, use) => {
    const created: number[] = [];
    async function publishOne(prefix = 'pub') {
      const title = uniq(prefix);
      const res = await wpApi.post('/wp-json/wp/v2/posts', {
        data: { title, status: 'publish', content: '<p>seed published</p>', categories: [seedCategory.id] }
      });
      if (!res.ok()) throw new Error(`Failed to publish: ${res.status()} ${await res.text()}`);
      const body = await res.json();
      created.push(body.id as number);
      return { id: body.id as number, title: body.title.rendered as string };
    }
    await use(publishOne);

    for (const id of created) {
      const del = await wpApi.delete(`/wp-json/wp/v2/posts/${id}?force=true`);
      if (!del.ok()) console.warn(`WARN: failed to delete published ${id}: ${del.status()} ${await del.text()}`);
    }
  }, { scope: 'test' }],
});

export { expect };
