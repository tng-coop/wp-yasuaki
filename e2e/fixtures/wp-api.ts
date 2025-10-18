// e2e/fixtures/wp-api.ts
import type { APIRequestContext, APIResponse } from '@playwright/test';

export type WPApi = {
  get: (path: string, opts?: { params?: Record<string, unknown> }) => Promise<APIResponse>;
  post: (path: string, opts?: { data?: unknown; params?: Record<string, unknown> }) => Promise<APIResponse>;
  delete: (path: string, opts?: { params?: Record<string, unknown> }) => Promise<APIResponse>;
};

function routeFor(path: string, params?: Record<string, unknown>) {
  const clean = String(path).replace(/^\/+/, '');
  let url = `/?rest_route=/wp/v2/${clean}`;
  if (params && Object.keys(params).length) {
    const qs = new URLSearchParams();
    for (const [k, v] of Object.entries(params)) {
      if (v === undefined || v === null) continue;
      qs.append(k, String(v));
    }
    url += (url.includes('?') ? '&' : '?') + qs.toString();
  }
  return url;
}

export async function createWpApi(context: APIRequestContext): Promise<WPApi> {
  return {
    get: (path, { params } = {}) => context.get(routeFor(path, params)),
    post: (path, { data, params } = {}) => context.post(routeFor(path, params), { data }),
    delete: (path, { params } = {}) => context.delete(routeFor(path, params)),
  };
}
