// e2e/fixtures/wp-api.ts
import type { APIRequestContext, RequestOptions } from '@playwright/test';

export async function createWpApi(request: any, siteBaseURL?: string) {
  const site = siteBaseURL || process.env.E2E_BASE_URL || '';
  if (!/^https?:\/\//.test(site)) {
    throw new Error('wpApi: baseURL not set. Set use.baseURL or E2E_BASE_URL.');
  }
  const base = site.replace(/\/$/, '');
  const auth = 'Basic ' + Buffer
    .from(`${process.env.WP_USERNAME}:${(process.env.WP_APP_PASSWORD || '').replace(/\s+/g, '')}`)
    .toString('base64');

  const ctx: APIRequestContext = await request.newContext({
    baseURL: base, // root site; we'll use ?rest_route=
    extraHTTPHeaders: { Authorization: auth, Accept: 'application/json' },
  });

  const norm = (p: string) => p.replace(/^\//, '');
  const q = (p: string) => `/?rest_route=/wp/v2/${norm(p)}`;
  const withJSON = (o?: RequestOptions) =>
    ({ ...o, headers: { 'Content-Type': 'application/json', ...(o?.headers ?? {}) } });

  return {
    get: (p: string, o?: RequestOptions) => ctx.get(q(p), o),
    post: (p: string, o?: RequestOptions) => ctx.post(q(p), withJSON(o)),
    put: (p: string, o?: RequestOptions) => ctx.put(q(p), withJSON(o)),
    delete: (p: string, o?: RequestOptions) => ctx.delete(q(p), o),
    raw: ctx,
  };
}
