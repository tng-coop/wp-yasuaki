// e2e/tests/wpapi-debug.spec.ts
import { test, request } from '@playwright/test';

const LOG = !!process.env.DEBUG_REST;
const log = (...a: any[]) => { if (LOG) console.log(...a); };

test('debug REST routing + auth', async ({}, testInfo) => {
  const base = String(testInfo.project.use.baseURL || '').replace(/\/$/, '');

  const api = await request.newContext({
    baseURL: `${base}/wp-json`,
    extraHTTPHeaders: {
      Authorization:
        'Basic ' +
        Buffer.from(
          `${process.env.WP_USERNAME}:${(process.env.WP_APP_PASSWORD || '').replace(/\s+/g, '')}`,
        ).toString('base64'),
      Accept: 'application/json',
    },
  });

  async function dump(label: string, path: string) {
    const r = await api.get(path, { failOnStatusCode: false });
    if (!LOG) return;
    const body = await r.text();
    log(`\n[${label}] ${r.status()} ${r.headers()['content-type'] || ''} -> ${r.url()}\n${body.slice(0, 300)}\n`);
  }

  await dump('index', '/');
  await dump('wp/v2', '/wp/v2/');
  await dump('users/me', '/wp/v2/users/me');

  // Permalink-bypass probe
  const root = await request.newContext({ baseURL: base });
  const r = await root.get('/?rest_route=/wp/v2/users/me', {
    failOnStatusCode: false,
    headers: {
      Authorization:
        'Basic ' +
        Buffer.from(
          `${process.env.WP_USERNAME}:${(process.env.WP_APP_PASSWORD || '').replace(/\s+/g, '')}`,
        ).toString('base64'),
      Accept: 'application/json',
    },
  });
  if (LOG) {
    const body = await r.text();
    log(`\n[rest_route users/me] ${r.status()} ${r.headers()['content-type'] || ''} -> ${r.url()}\n${body.slice(0, 300)}\n`);
  }
});
