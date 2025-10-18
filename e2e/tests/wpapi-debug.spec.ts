// e2e/tests/wpapi-debug.spec.ts
import { test, request } from '@playwright/test';

test('debug REST routing + auth', async ({}, testInfo) => {
  const base = String(testInfo.project.use.baseURL || '').replace(/\/$/, '');

  // Context that targets /wp-json (your wpApi equivalent)
  const api = await request.newContext({
    baseURL: `${base}/wp-json`,
    extraHTTPHeaders: {
      // TEMP: pull creds from env; strip spaces from app password
      Authorization: 'Basic ' + Buffer.from(
        `${process.env.WP_USERNAME}:${(process.env.WP_APP_PASSWORD || '').replace(/\s+/g, '')}`
      ).toString('base64'),
      Accept: 'application/json',
    },
  });

  async function dump(label: string, path: string) {
    const r = await api.get(path, { failOnStatusCode: false });
    const body = await r.text();
    // eslint-disable-next-line no-console
    console.log(`\n[${label}] ${r.status()} ${r.headers()['content-type'] || ''} -> ${r.url()}\n${body.slice(0, 300)}\n`);
  }

  // 1) Does /wp-json even return JSON?
  await dump('index', '/');

  // 2) Is the /wp/v2 namespace reachable?
  await dump('wp/v2', '/wp/v2/');

  // 3) What do we get from /users/me with Basic auth?
  await dump('users/me', '/wp/v2/users/me');

  // 4) Bypass pretty permalinks: use ?rest_route= (avoids rewrite issues)
  const root = await request.newContext({ baseURL: base });
  const r = await root.get('/?rest_route=/wp/v2/users/me', { failOnStatusCode: false, headers: {
    Authorization: 'Basic ' + Buffer.from(
      `${process.env.WP_USERNAME}:${(process.env.WP_APP_PASSWORD || '').replace(/\s+/g, '')}`
    ).toString('base64'),
    Accept: 'application/json',
  }});
  const body = await r.text();
  // eslint-disable-next-line no-console
  console.log(`\n[rest_route users/me] ${r.status()} ${r.headers()['content-type'] || ''} -> ${r.url()}\n${body.slice(0, 300)}\n`);
});
